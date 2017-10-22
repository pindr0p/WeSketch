﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WeSketchSharedDataModels;

namespace WeSketch
{
    /// <summary>
    /// Interaction logic for WeSketchApp.xaml
    /// </summary>
    public partial class WeSketchApp : Page
    {
        private WeSketchRestRequests _rest = new WeSketchRestRequests();
        private WeSketchSignalrClient _client = new WeSketchSignalrClient();
        
        public WeSketchApp()
        {
            InitializeComponent();

            ik.StrokeCollected += Ik_StrokeCollected;
            ik.StrokeErasing += Ik_StrokeErasing;

            _client.UserAuthenticated(WeSketchClientData.Instance.User.UserID);
            _client.JoinBoardGroup(WeSketchClientData.Instance.User.Board.BoardID);
            _client.BoardInvitationReceivedEvent += BoardInvitationReceivedEvent;
            _client.BoardChangedEvent += _client_BoardChangedEvent;
            _client.StrokesReceivedEvent += _client_StrokesReceivedEvent;
            _client.StrokeRequestReceivedEvent += _client_StrokeRequestReceivedEvent;
            _client.clearButton_Click += clearButton_Click;
            _client.closeButton_Click += closeButton_Click;
        }

        private void Ik_StrokeErasing(object sender, InkCanvasStrokeErasingEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void _client_StrokeRequestReceivedEvent(string requestingUser)
        {
            // TODO: Send the board strokes to the requesting user.
            throw new NotImplementedException();
        }

        private void Ik_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void _client_StrokesReceivedEvent(System.Windows.Ink.StrokeCollection strokes)
        {
            ik.Strokes.Add(strokes);
        }

        private void _client_BoardChangedEvent(Guid boardId)
        {
            WeSketchClientData.Instance.User.Board.BoardID = boardId;
            ik.Strokes.Clear();
            _client.RequestStrokes(WeSketchClientData.Instance.User.UserName, WeSketchClientData.Instance.User.Board.BoardID);
        }

        private void BoardInvitationReceivedEvent(string user, Guid boardId)
        {
            MessageBoxResult result = MessageBox.Show($"User {user} invited you to their board.  Would you like to join?", "Join board?", MessageBoxButton.YesNo);
            if(result == MessageBoxResult.Yes)
            {
                _client.LeaveBoardGroup(WeSketchClientData.Instance.User.Board.BoardID);
                _client.JoinBoardGroup(boardId);
            }
        }

        private void clearButton_Click(object sender, RoutedEventArgs e)
        {
            this.mainInkCanvas.Strokes.Clear();
            MessageBox.Show("Clear button pressed.");
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("WeSketch close button pressed.");
            this.Close();
        }
    }
}
