﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Controls;
using Newtonsoft.Json;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Hubs;
using System.Configuration;
using WeSketchSharedDataModels;
using System.Windows.Media;

namespace WeSketch
{
    /// <summary>
    /// Communication portal for the client to send and receive messages from the hub running on WeSketchAPI.
    /// </summary>
    public class WeSketchSignalrClient
    {
        /// <summary>
        /// Fires when the users board is changed.
        /// </summary>
        /// <param name="boardId">The board identifier.</param>
        public delegate void BoardChangedEventHandler(Guid boardId);
        public event BoardChangedEventHandler BoardChangedEvent;
        
        /// <summary>
        /// Fires when an invitation is received to join a board.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="boardId">The board identifier.</param>
        public delegate void BoardInvitationReceivedEventHandler(string user, Guid boardId);
        public event BoardInvitationReceivedEventHandler BoardInvitationReceivedEvent;

        /// <summary>
        /// Fires when the users board has changed and they are no longer the owner of the board.
        /// </summary>
        /// <param name="owner">if set to <c>true</c> [owner].</param>
        public delegate void BoardOwnerChanged(bool owner);
        public event BoardOwnerChanged BoardOwnerChangedEvent;

        /// <summary>
        /// Fires when a user requests the connected users.
        /// </summary>
        /// <param name="user">The user.</param>
        public delegate void ConnectedUsersRequestReceivedEventHandler(string user);
        public event ConnectedUsersRequestReceivedEventHandler ConnectedUsersRequestReceivedEvent;

        /// <summary>
        /// Fires when a list of connected users has been received.
        /// </summary>
        /// <param name="connectedUsers">The connected users.</param>
        public delegate void ConnectedUsersReceivedEventHandler(List<ConnectedUser> connectedUsers);
        public event ConnectedUsersReceivedEventHandler ConnectedUsersReceivedEvent;

        /// <summary>
        /// Fires when the hub state was changed to disconnected.
        /// </summary>
        public delegate void HubDisconnectedEventHandler();
        public event HubDisconnectedEventHandler HubDisconnectedEvent;
        
        /// <summary>
        /// Fires when the hub threw an exception.
        /// </summary>
        /// <param name="e">The exception.</param>
        public delegate void HubErrorEventHandler(Exception e);
        public event HubErrorEventHandler HubErrorEvent;

        /// <summary>
        /// Fires when the hub state was changes to reconnecting.
        /// </summary>
        public delegate void HubReconnectingEventHandler();
        public event HubReconnectingEventHandler HubReconnectingEvent;

        /// <summary>
        /// Fires when the user has been kicked fromt he board.
        /// </summary>
        public delegate void KickedFromBoardEventHandler();
        public event KickedFromBoardEventHandler KickedFromBoardEvent;

        /// <summary>
        /// Fires when strokes are received.
        /// </summary>
        /// <param name="strokes">The strokes.</param>
        public delegate void StrokesReceivedEventHandler(System.Windows.Ink.StrokeCollection strokes);
        public event StrokesReceivedEventHandler StrokesReceivedEvent;

        /// <summary>
        /// Fires when strokes to erase are received.
        /// </summary>
        /// <param name="id">The stroke id to erase.</param>
        public delegate void StrokeEraseReceivedEventHandler(Guid id);
        public event StrokeEraseReceivedEventHandler StrokeErasedEvent;

        /// <summary>
        /// Fires when the given user requests the board strokes.
        /// </summary>
        /// <param name="requestingUser">The requesting user.</param>
        public delegate void StrokeRequestReceivedEventHandler(string requestingUser);
        public event StrokeRequestReceivedEventHandler StrokeRequestReceivedEvent;

        /// <summary>
        /// Fires when the board is cleared.
        /// </summary>
        public delegate void StrokeClearEventHandler();
        public event StrokeClearEventHandler StrokeClearEvent;

        /// <summary>
        /// Fires when a user joins the board.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="color">The color.</param>
        public delegate void UserJoinedBoardEventHandler(ConnectedUser user);
        public event UserJoinedBoardEventHandler UserJoinedBoardEvent;

        /// <summary>
        /// Fires when a user leaves the board.
        /// </summary>
        /// <param name="user">The user that left the board.</param>
        public delegate void UserLeftBoardEventHandler(string user);
        public event UserLeftBoardEventHandler UserLeftBoardEvent;

        /// <summary>
        /// Fires when a connected user changes their pen color.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="color">The color.</param>
        public delegate void UserColorChangedEventHandler(string user, string color);
        public event UserColorChangedEventHandler UserColorChangedEvent;

        /// <summary>
        /// Fires when the hub state was changed to connected.
        /// </summary>
        public delegate void HubConnectedEventHandler();
        public event HubConnectedEventHandler HubConnectedEvent;
        
#if DEBUG
        private string _url = ConfigurationManager.AppSettings["SignalrDebugUrl"];
#else
        private string _url = ConfigurationManager.AppSettings["SignalrUrl"];
#endif
        private HubConnection _hub;
        private IHubProxy _hubProxy;

        private Queue<Action> _queuedActions = new Queue<Action>();

        /// <summary>
        /// This allows messages to be sent to and from the WeSketch SignalR hub.
        /// </summary>
        public WeSketchSignalrClient()
        {
            _hub = new HubConnection(_url);
            _hub.Reconnected += HubReconnected;
            _hub.Error += HubError;
            _hub.StateChanged += HubStateChanged;
            _hubProxy = _hub.CreateHubProxy("WeSketchSignalRHub");

            _hubProxy.On<Guid>("KickedFromBoard", boardId => KickedFromBoard(boardId));
            _hubProxy.On("ReceiveConnectedUsersRequest", user => ReceiveConnectedUsersRequest(user));
            _hubProxy.On("ReceiveConnectedUsers", users => ReceiveConnectedUsers(JsonConvert.DeserializeObject<List<ConnectedUser>>(users)));
            _hubProxy.On<string, Guid>("ReceiveInvitation", (user, boardId) => ReceiveInvitation(user, boardId));
            _hubProxy.On("ReceiveStrokes", strokes => ReceiveStrokes(strokes));
            _hubProxy.On("ReceiveStrokeRequest", user => ReceiveStrokeRequest(user));
            _hubProxy.On("ReceiveStrokeToErase", stroke => ReceiveStrokeToErase(stroke));
            _hubProxy.On("StrokesClearedReceived", () => StrokesClearedReceived());
            _hubProxy.On<string, string>("UserColorChanged", (user, color) => UserColorChanged(user, color));
            _hubProxy.On<Guid, bool>("UserBoardSetToDefault", (boardId, clearStrokes) => UserBoardSetToDefault(boardId, clearStrokes));
            _hubProxy.On("UserLeftBoard", user => UserLeftBoard(user));
            _hubProxy.On("UserJoinedBoard", connectedUser => UserJoinedBoard(JsonConvert.DeserializeObject<ConnectedUser>(connectedUser)));
            _hub.Start().Wait();
        }

        /// <summary>
        /// Changes the color of the user.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="color">The color.</param>
        public void ChangeUserColor(string userName, string color)
        {

            if (userName == string.Empty)
            {
                throw new Exception(message: $"Error: A username is required.");
            }
            if (color == string.Empty)
            {
                throw new Exception(message: $"Error: A color is required.");
            }
            InvokeHubDependantAction(() =>
            _hubProxy.Invoke<Task>("ChangeUserColor", userName, color));
        }

        /// <summary>
        /// Joins the board group.
        /// </summary>
        /// <param name="userName">Name of the user.</param>
        /// <param name="color">The color.</param>
        /// <param name="boardId">The board identifier.</param>
        public void JoinBoardGroup(string userName, string color, Guid boardId)
        {
            if (userName == string.Empty)
            {
                throw new Exception(message: $"Error: A username is required.");
            }
            if (color == string.Empty)
            {
                throw new Exception(message: $"Error: A color is required.");
            }
            if (boardId == null)
            {
                throw new Exception(message: $"Error: ID required.");
            }
            InvokeHubDependantAction(() =>
            {
                _hubProxy.Invoke<Task>("JoinBoardGroup", userName, color, boardId);
                BoardChangedEvent?.Invoke(boardId);
                BoardOwnerChangedEvent?.Invoke(false);
            });
        }

        /// <summary>
        /// Kicks the user from board.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="boardId">The board identifier.</param>
        public void KickUserFromBoard(string user, Guid boardId)
        {
            if (user == string.Empty)
            {
                throw new Exception(message: $"Error: A user is required.");
            }
            if (boardId == null)
            {
                throw new Exception(message: $"Error: ID required.");
            }
            InvokeHubDependantAction(() =>
            _hubProxy.Invoke<Task>("KickUserFromBoard", user, boardId));
        }

        /// <summary>
        /// Leaves the board group.
        /// </summary>
        /// <param name="user">The user leaving the board.</param>
        /// <param name="boardId">The board that the user is leaving.</param>
        public void LeaveBoardGroup(string user, Guid boardId, string color)
        {
            if (user == string.Empty)
            {
                throw new Exception(message: $"Error: A user is required.");
            }
            if (boardId == null)
            {
                throw new Exception(message: $"Error: ID required.");
            }
            if (color == string.Empty)
            {
                throw new Exception(message: $"Error: A color is required.");
            }
            InvokeHubDependantAction(() =>
            _hubProxy.Invoke<Task>("LeaveBoardGroup", user, boardId, color));
        }
        
        /// <summary>
        /// Receives the strokes and invokes the StrokesReceivedEvent.
        /// </summary>
        /// <param name="strokes">The strokes.</param>
        public void ReceiveStrokes(string serIalizedtrokes)
        {
            StrokesReceivedEvent?.Invoke(GetStrokeCollection(JsonConvert.DeserializeObject<List<BoardPointCollection>>(serIalizedtrokes)));
        }

        /// <summary>
        /// Requests the users connected to the board.
        /// </summary>
        /// <param name="user">The user making the request.</param>
        /// <param name="boardId">The board identifier.</param>
        public void RequestConnectedUsers(string user, Guid boardId)
        {
            if (user == string.Empty)
            {
                throw new Exception(message: $"Error: A user is required.");
            }
            if (boardId == null)
            {
                throw new Exception(message: $"Error: ID required.");
            }
            InvokeHubDependantAction(() => _hubProxy.Invoke("RequestConnectedUsers", user, boardId));
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
        /// Sends the connected users to the specified user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="connectedUsers">The connected users.</param>
        public void SendConnectedUsersToUser(string user, List<ConnectedUser> connectedUsers)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("SendConnectedUsersToUser", user, JsonConvert.SerializeObject(connectedUsers)));
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
                InvokeHubDependantAction(() => _hubProxy.Invoke("SendStrokesToGroup", boardId, JsonConvert.SerializeObject(GetBoardPointCollectionList(strokes))));
            }
        }

        /// <summary>
        /// Sends the stroke to erase.
        /// </summary>
        /// <param name="stroke">The stroke.</param>
        public void SendStrokeToErase(System.Windows.Ink.Stroke stroke, Guid boardId)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("SendStrokeToErase", JsonConvert.SerializeObject(GetBoardPointCollection(stroke)), boardId));
        }

        /// <summary>
        /// Sends the strokes to user.
        /// </summary>
        /// <param name="user">The user identifier.</param>
        /// <param name="strokes">The strokes.</param>
        public void SendStrokesToUser(string user, System.Windows.Ink.StrokeCollection strokes)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("SendStrokesToUser", user, JsonConvert.SerializeObject(GetBoardPointCollectionList(strokes))));
        }
        
        /// <summary>
        /// Hub will be sent that strokes are cleared.
        /// </summary>
        /// <param name="boardId"></param>
        public void StrokesClearedSend(Guid boardId)
        {
            InvokeHubDependantAction(() => _hubProxy.Invoke("RequestClearBoardStrokes", boardId));
        }
        
        /// <summary>
        /// User is authenticated. It lets the hub know and the user is then
        /// placed in their own unique group.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        public void UserAuthenticated(Guid userId)
        {
            if (userId == null)
            {
                throw new Exception(message: $"Error: User ID required.");
            }
            InvokeHubDependantAction(() => _hubProxy.Invoke("UserAuthenticated", userId));
        }

        /// <summary>
        /// Gets the board point collection list.
        /// </summary>
        /// <param name="strokes">The strokes.</param>
        /// <returns>A list of board point collections.</returns>
        private List<BoardPointCollection> GetBoardPointCollectionList(System.Windows.Ink.StrokeCollection strokes)
        {
            List<BoardPointCollection> bpcList = new List<BoardPointCollection>();

            strokes.ToList().ForEach(stroke =>
            {
                bpcList.Add(GetBoardPointCollection(stroke));
            });

            return bpcList;
        }

        /// <summary>
        /// Gets the board point collection.  This allows serializable messages to be sent to the server.
        /// </summary>
        /// <param name="stroke">The stroke.</param>
        /// <returns>A new board point collection.</returns>
        private BoardPointCollection GetBoardPointCollection(System.Windows.Ink.Stroke stroke)
        {
            var bpc = new BoardPointCollection();
            bpc.Color = stroke.DrawingAttributes.Color.ToString();
            var customProperties = stroke.GetPropertyDataIds();
            bpc.ID = customProperties.First();
            bpc.User = stroke.GetPropertyData(customProperties.First()).ToString();
            bpc.BrushWidth = stroke.DrawingAttributes.Width;
            bpc.BrushHeight = stroke.DrawingAttributes.Height;
            stroke.StylusPoints.ToList().ForEach(point =>
            {
                bpc.Points.Add(new BoardPoint()
                {  
                    X = point.X,
                    Y = point.Y,
                    PressureFactor = point.PressureFactor
                });
            });
            return bpc;
        }

        /// <summary>
        /// Converts a Llist of board point collections into a stroke collection.
        /// </summary>
        /// <param name="bpcList">The BPC list.</param>
        /// <returns></returns>
        private System.Windows.Ink.StrokeCollection GetStrokeCollection(List<BoardPointCollection> bpcList)
        {
            var strokes = new System.Windows.Ink.StrokeCollection();
            bpcList.ForEach(bpc =>
            {
                strokes.Add(GetStroke(bpc));
            });
            return strokes;
        }

        /// <summary>
        /// Converts a board point collection into a stroke.
        /// </summary>
        /// <param name="bpc">The BPC.</param>
        /// <returns>A stroke created from the given board point collection.</returns>
        private System.Windows.Ink.Stroke GetStroke(BoardPointCollection bpc)
        {
            System.Windows.Input.StylusPointCollection spc = new System.Windows.Input.StylusPointCollection();
            
            bpc.Points.ForEach(point =>
            {
                spc.Add(new System.Windows.Input.StylusPoint(point.X, point.Y, point.PressureFactor));
            });
            var stroke = new System.Windows.Ink.Stroke(spc, new System.Windows.Ink.DrawingAttributes() { Color = (Color)ColorConverter.ConvertFromString(bpc.Color) });
            stroke.AddPropertyData(bpc.ID, bpc.User);
            stroke.DrawingAttributes.Height = bpc.BrushHeight;
            stroke.DrawingAttributes.Width = bpc.BrushWidth;
            return stroke;
        }

        /// <summary>
        /// Notifies the client of a hub exception.
        /// </summary>
        /// <param name="obj">The exception object.</param>
        private void HubError(Exception obj)
        {
            HubErrorEvent?.Invoke(obj);
        }

        /// <summary>
        /// After a hub reconnects after a disconnect it will dequeue all enqueued actions that took place while
        /// the hub state was disconnected.
        /// </summary>
        private void HubReconnected()
        {
            lock (_queuedActions)
            {
                while (_queuedActions.Any() && _hub.State == ConnectionState.Connected)
                {
                    Action act = _queuedActions.Dequeue();
                    act.Invoke();
                }
            }
        }

        /// <summary>
        /// Triggers events when the state changes to notify the client.
        /// </summary>
        /// <param name="obj">The state changed object.</param>
        private void HubStateChanged(StateChange obj)
        {
            switch (obj.NewState)
            {
                case ConnectionState.Connected:
                    HubConnectedEvent?.Invoke();
                    break;

                case ConnectionState.Disconnected:
                    HubDisconnectedEvent?.Invoke();
                    break;

                case ConnectionState.Reconnecting:
                    HubReconnectingEvent?.Invoke();
                    break;
            }
        }
        
        /// <summary>
        /// This sends a message to the server or queues it if it is not currently connected.
        /// </summary>
        /// <param name="action">The action to send or enqueue.</param>
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
        
        /// <summary>
        /// The client user has been kicked from the board by the board owner.
        /// </summary>
        /// <param name="boardId">The board identifier.</param>
        private void KickedFromBoard(Guid boardId)
        {
            LeaveBoardGroup(WeSketchClientData.Instance.User.UserName, boardId, WeSketchClientData.Instance.Color);
            KickedFromBoardEvent?.Invoke();
        }

        /// <summary>
        /// Receives the connected users.
        /// </summary>
        /// <param name="connectedUsers">The connected users.</param>
        private void ReceiveConnectedUsers(List<ConnectedUser> connectedUsers)
        {
            ConnectedUsersReceivedEvent?.Invoke(connectedUsers);
        }

        /// <summary>
        /// Receives a request to send all connected users to the specified user.
        /// </summary>
        /// <param name="user">The user.</param>
        private void ReceiveConnectedUsersRequest(string user)
        {
            ConnectedUsersRequestReceivedEvent?.Invoke(user);
        }
        
        /// <summary>
        /// Receives the invitation.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="boardId">The board identifier.</param>
        private void ReceiveInvitation(string user, Guid boardId)
        {
            BoardInvitationReceivedEvent?.Invoke(user, boardId);
        }

        /// <summary>
        /// Receives the stroke request.
        /// </summary>
        /// <param name="requestingUser">The requesting user.</param>
        private void ReceiveStrokeRequest(string requestingUser)
        {
            StrokeRequestReceivedEvent?.Invoke(requestingUser);
        }

        /// <summary>
        /// Receivers the stroke to erase.
        /// </summary>
        /// <param name="serializedStroke">The serialized stroke.</param>
        private void ReceiveStrokeToErase(string serializedStroke)
        {
            BoardPointCollection bpc = JsonConvert.DeserializeObject<BoardPointCollection>(serializedStroke);
            StrokeErasedEvent?.Invoke(bpc.ID);
        }
        
        /// <summary>
        /// Checks and lets the hub know when strokes cleared event is received.
        /// </summary>
        private void StrokesClearedReceived()
        {
            StrokeClearEvent?.Invoke();
        }

        /// <summary>
        /// The users board changed on the server.  This notifies the user that their board changed.
        /// </summary>
        /// <param name="boardId">The board identifier.</param>
        private void UserBoardSetToDefault(Guid boardId, bool clearStrokes)
        {
            BoardChangedEvent?.Invoke(boardId);
            BoardOwnerChangedEvent?.Invoke(true);
            if (clearStrokes)
            {
                StrokeClearEvent?.Invoke();
            }
        }

        /// <summary>
        /// Informs the client that the specified user changed their color.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="color">The color.</param>
        private void UserColorChanged(string user, string color)
        {
            UserColorChangedEvent?.Invoke(user, color);
        }

        /// <summary>
        /// Given user has left the board.
        /// </summary>
        /// <param name="user">The user.</param>
        private void UserLeftBoard(string user)
        {
            UserLeftBoardEvent?.Invoke(user);
        }

        /// <summary>
        /// User the joined board.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="color">The color.</param>
        private void UserJoinedBoard(ConnectedUser user)
        {
            UserJoinedBoardEvent?.Invoke(user);
        }
        
        /// <summary>
        /// Clean up
        /// </summary>
        public void Dispose()
        {
            if(_hub.State == ConnectionState.Connected)
            {
                LeaveBoardGroup(WeSketchClientData.Instance.User.UserName, WeSketchClientData.Instance.User.Board.BoardID, WeSketchClientData.Instance.Color);
                _hubProxy.Invoke("RemoveFromGroups", WeSketchClientData.Instance.User.UserName, WeSketchClientData.Instance.User.Board.BoardID);
            }
            _hub.Dispose();
            _hub = null;
        }
    }
}
