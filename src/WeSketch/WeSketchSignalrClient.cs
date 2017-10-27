﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Controls;
using Newtonsoft.Json;
using Microsoft.AspNet.SignalR.Client;
using System.Configuration;
using WeSketchSharedDataModels;

namespace WeSketch
{
    /// <summary>
    /// Communication portal for the client to send and receive messages from the hub running on WeSketchAPI.
    /// </summary>
    class WeSketchSignalrClient
    {
        public delegate void BoardChangedEventHandler(Guid boardId);
        public event BoardChangedEventHandler BoardChangedEvent;

        public delegate void BoardInvitationReceivedEventHandler(string user, Guid boardId);
        public event BoardInvitationReceivedEventHandler BoardInvitationReceivedEvent;

        public delegate void StrokesReceivedEventHandler(System.Windows.Ink.StrokeCollection strokes);
        public event StrokesReceivedEventHandler StrokesReceivedEvent;

        public delegate void StrokeEraseReceivedEventHandler(System.Windows.Ink.Stroke stroke);
        public event StrokeEraseReceivedEventHandler StrokeErasedEvent;

        public delegate void StrokeRequestReceivedEventHandler(string requestingUser);
        public event StrokeRequestReceivedEventHandler StrokeRequestReceivedEvent;

        public delegate void StrokeClearEventHandler();
        public event StrokeClearEventHandler StrokeClearEvent; //strokeclearevent instance of strokeclearevent handler object
#if DEBUG
        private string _url = "http://localhost/WeSketchAPI/signalr";
#else
        private string _url = ConfigurationManager.AppSettings["url"];
#endif
        private HubConnection _hub;
        private IHubProxy _hubProxy;

        private Queue<Action> _queuedActions = new Queue<Action>();



        public WeSketchSignalrClient()
        {
            _hub = new HubConnection(_url);
            _hub.Reconnected += Hub_Reconnected;
            _hub.Reconnecting += Hub_Reconnecting;
            _hub.Closed += Hub_Closed;
            _hub.ConnectionSlow += Hub_ConnectionSlow;
            _hub.Error += Hub_Error;
            _hub.StateChanged += Hub_StateChanged;
            _hubProxy = _hub.CreateHubProxy("WeSketchSignalRHub");

            _hubProxy.On<string, Guid>("ReceiveInvitation", (user, boardId) => ReceiveInvitation(user, boardId));
            _hubProxy.On("ReceiveStrokes", strokes => ReceiveStrokes(strokes));
            _hubProxy.On("ReceiveStrokeRequest", user => ReceiveStrokeRequest(user));
            _hubProxy.On("ReceiverStrokeToErase", stroke => ReceiveStrokeToErase(stroke));

            _hub.Start().Wait();
        }

        private void Hub_StateChanged(StateChange obj)
        {
        }

        private void Hub_Error(Exception obj)
        {
        }

        private void Hub_ConnectionSlow()
        {
        }

        private void Hub_Closed()
        {
        }

        private void Hub_Reconnecting()
        {
        }

        /// <summary>
        /// After a hub reconnects after a disconnect it will dequeue all enqueued actions that took place while
        /// the hub state was disconnected.
        /// </summary>
        private void Hub_Reconnected()
        {
            lock (_queuedActions)
            {
                if (_queuedActions.Any())
                {
                    while (_queuedActions.Peek() != null && _hub.State == ConnectionState.Connected)
                    {
                        Action act = _queuedActions.Dequeue();
                        act.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Receives the invitation.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="boardId">The board identifier.</param>
        public void ReceiveInvitation(string user, Guid boardId)
        {
            BoardInvitationReceivedEvent?.Invoke(user, boardId);
        }
        /// <summary>
        /// Receives the strokes and invokes the StrokesReceivedEvent.
        /// </summary>
        /// <param name="strokes">The strokes.</param>
        public void ReceiveStrokes(string serIalizedtrokes)
        {
            var strokes = new System.Windows.Ink.StrokeCollection();
            List<BoardPointCollection> bpc = JsonConvert.DeserializeObject<List<BoardPointCollection>>(serIalizedtrokes);

            bpc.ForEach(collection =>
            {
                System.Windows.Input.StylusPointCollection spc = new System.Windows.Input.StylusPointCollection();
                collection.Points.ForEach(point =>
                {
                    spc.Add(new System.Windows.Input.StylusPoint(point.X, point.Y, point.PressureFactor));
                });

                strokes.Add(new System.Windows.Ink.Stroke(spc));
            });
                
            StrokesReceivedEvent?.Invoke(strokes);
        }

        /// <summary>
        /// Receivers the stroke to erase.
        /// </summary>
        /// <param name="serializedStroke">The serialized stroke.</param>
        public void ReceiveStrokeToErase(string serializedStroke)
        {
            StrokeErasedEvent?.Invoke(JsonConvert.DeserializeObject<System.Windows.Ink.Stroke>(serializedStroke));
        }

        /// <summary>
        /// Sends the stroke to erase.
        /// </summary>
        /// <param name="stroke">The stroke.</param>
        public void SendStrokeToErase(System.Windows.Ink.Stroke stroke)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("SendStrokeToErase", JsonConvert.SerializeObject(stroke)));
        }

        /// <summary>
        /// Receives the stroke request.
        /// </summary>
        /// <param name="requestingUser">The requesting user.</param>
        public void ReceiveStrokeRequest(string requestingUser)
        {
            StrokeRequestReceivedEvent?.Invoke(requestingUser);
        }

        /// <summary>
        /// Sends the stroke.
        /// </summary>
        /// <param name="boardId">The board identifier.</param>
        /// <param name="stroke">The stroke.</param>
        public void SendStroke(Guid boardId, System.Windows.Ink.Stroke stroke)
        {
            InvokeHubDependantAction(() =>
            SendStrokes(boardId, new System.Windows.Ink.StrokeCollection()
            {
                stroke
            }));
        }

        /// <summary>
        /// Sends the stroke to the board.
        /// </summary>
        /// <param name="boardId">The board identifier.</param>
        /// <param name="stroke">The stroke.</param>
        public void SendStrokes(Guid boardId, System.Windows.Ink.StrokeCollection strokes)
        {
            if (strokes.Any())
            {
                List<BoardPointCollection> points = new List<BoardPointCollection>();
                strokes.ToList().ForEach(stroke =>
                {
                    points.Add(new BoardPointCollection());
                    stroke.StylusPoints.ToList().ForEach(point =>
                    {
                        points.Last().Points.Add(new BoardPoint()
                        {
                            X = point.X,
                            Y = point.Y,
                            PressureFactor = point.PressureFactor
                        });
                    });
                });
                InvokeHubDependantAction(() => _hubProxy.Invoke("SendStrokesToGroup", boardId, JsonConvert.SerializeObject(points)));
            }
        }

        /// <summary>
        /// Sends the strokes to user.
        /// </summary>
        /// <param name="user">The user identifier.</param>
        /// <param name="strokes">The strokes.</param>
        public void SendStrokesToUser(string user, System.Windows.Ink.StrokeCollection strokes)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("SendStrokesToUser", user, JsonConvert.SerializeObject(strokes)));
        }

        /// <summary>
        /// Requests the strokes from the board.
        /// </summary>
        /// <param name="userId">The user identifier requesting the strokes.</param>
        /// <param name="boardId">The board identifier.</param>
        public void RequestStrokes(string user, Guid boardId)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("RequestStrokes", user, boardId));
        }

        /// <summary>
        /// Joins the board group.
        /// </summary>
        /// <param name="boardId">The board identifier.</param>
        public void JoinBoardGroup(Guid boardId)
        {
            _hubProxy.Invoke<Task>("JoinBoardGroup", boardId);
            BoardChangedEvent?.Invoke(boardId);
        }

        /// <summary>
        /// Leaves the board group.
        /// </summary>
        /// <param name="boardId">The board identifier.</param>
        public void LeaveBoardGroup(Guid boardId)
        {
            _hubProxy.Invoke<Task>("LeaveBoardGroup", boardId);
        }


        /// <summary>
        /// User is authenticated. It lets the hub know and the user is then
        /// placed in their own unique group.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        public void UserAuthenticated(Guid userId)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("UserAuthenticated", userId));
        }


        public void StrokesClearedReceived()
        {
            StrokeClearEvent?.Invoke();

            //StrokeRequestReceivedEvent?.Invoke(requestingUser);
        }

        public void StrokesClearedSend(Guid boardId)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("RequestClearBoardStrokes", boardId));
        }

        private void InvokeHubDependantAction(Action action)
        {
            if (_hub.State == ConnectionState.Connected)
            {
                action.Invoke();
            }
            else
            {
                _queuedActions.Enqueue(action);
            }
        }

        public void Dispose()
        {
            _hub.Dispose();
            _hub = null;
        }
    }
}
