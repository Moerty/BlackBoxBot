﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using Aiva.Core.Models;
using TwitchLib.Events.Client;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace Aiva.Extensions.Chat {

    /*
     * Can show Messages for GUI
     * Store Messages in Database
     * Write Messages from GUI
     */
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class Chat {

        public ObservableCollection<Models.Chat.Messages> Messages { get; set; }
        public ObservableCollection<Models.Chat.Viewers> Viewers { get; set; }
        public Models.Chat.Viewers SelectedViewer { get; set; }

        public Chat() {
            Messages = new ObservableCollection<Models.Chat.Messages>();
            Viewers = new ObservableCollection<Models.Chat.Viewers>();
            Messages.CollectionChanged += MessagesCountCheck;
            Core.AivaClient.Instance.AivaTwitchClient.OnMessageReceived += ChatMessageReceived;
            Core.AivaClient.Instance.Tasks.OnModeratorsReceivedEvent += ModeratorsReceived;
            Core.AivaClient.Instance.AivaTwitchClient.OnUserLeft += RemoveViewerFromViewers;
            Core.AivaClient.Instance.AivaTwitchClient.OnUserJoined += AivaTwitchClient_OnUserJoined;
            Core.AivaClient.Instance.AivaTwitchClient.OnExistingUsersDetected += AivaTwitchClient_OnExistingUsersDetected;
        }

        private void AivaTwitchClient_OnExistingUsersDetected(object sender, OnExistingUsersDetectedArgs e) {
            foreach (var user in e.Users) {
                var twitchUser = TwitchLib.TwitchAPI.Users.v5.GetUserByNameAsync(user).Result;

                if (twitchUser != null) {
                    OnNewUserFound(twitchUser.Matches[0].Name, twitchUser.Matches[0].Id);
                }
            }
        }

        private void AivaTwitchClient_OnUserJoined(object sender, OnUserJoinedArgs e) {
            var twitchUser = TwitchLib.TwitchAPI.Users.v5.GetUserByNameAsync(e.Username).Result;

            if (twitchUser != null) {
                OnNewUserFound(twitchUser.Matches[0].Name, twitchUser.Matches[0].Id);
            }
        }

        private void OnNewUserFound(string name, string id) {

            if (Viewers.Any(v => String.Compare(v.TwitchID, id) == 0)) // check if user is already in List
                return;

            var IsUserSubscriber = TwitchLib.TwitchAPI.Subscriptions.v3.ChannelHasUserSubscribedAsync(Core.AivaClient.Instance.Channel, name).Result;

            var rnd = new Random();
            var viewer = new Models.Chat.Viewers {
                Name = name,
                TwitchID = id,
                IsSub = IsUserSubscriber != null ? true : false,
                Type = IsUserSubscriber != null ? nameof(Models.Chat.SortDirectionListView.Subscriber)
                                            : nameof(Models.Chat.SortDirectionListView.Viewer),
                ChatNameColor = Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256))
                //IsMod = will be filled from the event "ModeratoersReceived"
            };

            Application.Current.Dispatcher.Invoke(() => { Viewers.Add(viewer); });


            // Get Channel Moderators to fire "ModeratorsReceived"
            Core.AivaClient.Instance.AivaTwitchClient.GetChannelModerators(Core.AivaClient.Instance.Channel);
        }

        /// <summary>
        /// Check if a joined
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModeratorsReceived(object sender, OnModeratorsReceivedArgs e) {
            var matches = Viewers.Where(v => e.Moderators.Contains(v.Name));

            foreach (var match in matches) {
                match.IsMod = true;
                match.Type = nameof(Models.Chat.SortDirectionListView.Mod);
            }
        }

        /// <summary>
        /// Remove User from Viewers
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveViewerFromViewers(object sender, OnUserLeftArgs e) {
            var viewer = Viewers.SingleOrDefault(u => u.Name == e.Username);

            if (viewer != null) {

                Application.Current.Dispatcher.Invoke(() => {
                    Viewers.Remove(viewer);
                });
            }
        }

        /// <summary>
        /// Remove Message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MessagesCountCheck(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add) {
                if (Messages.Count >= 1000) {
                    Application.Current.Dispatcher.Invoke(() => {
                        Messages.RemoveAt(0);
                    });
                }
            }
        }

        /// <summary>
        /// Fires when ChatMessage Received
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatMessageReceived(object sender, OnMessageReceivedArgs e) {
            var rnd = new Random();
            var AddModel = new Models.Chat.Messages {
                IsUserMod = e.ChatMessage.IsModerator,
                IsUserSub = e.ChatMessage.IsSubscriber,
                TwitchID = e.ChatMessage.UserId,
                Username = e.ChatMessage.Username,
                Message = e.ChatMessage.Message,
                TimeStamp = DateTime.Now,
                Color = e.ChatMessage.Color.IsEmpty ? GetChatColor(e.ChatMessage.UserId) : e.ChatMessage.Color
            };

            Application.Current.Dispatcher.Invoke(() => {
                Messages.Add(AddModel);
            });


            // Save in Database
            StoreIndatabase(AddModel);
        }

        private Color GetChatColor(string userId) {
            var viewer = Viewers.SingleOrDefault(v => String.Compare(v.TwitchID, userId, true) == 0);

            if (viewer != null) {
                return viewer.ChatNameColor;
            } else {
                var rnd = new Random();
                return Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256));
            }
        }

        /// <summary>
        /// Store Message in Database
        /// </summary>
        /// <param name="AddModel"></param>
        private static void StoreIndatabase(Models.Chat.Messages AddModel) {
            using (var context = new Core.Storage.StorageEntities()) {
                context.Chat.Add(
                    new Core.Storage.Chat {
                        TwitchID = AddModel.TwitchID,
                        ChatMessage = AddModel.Message,
                        Timestamp = AddModel.TimeStamp,
                    });

                context.SaveChanges();
            }
        }

        /// <summary>
        /// Send a Message to the Chat
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(string message) {
            Core.AivaClient.Instance.AivaTwitchClient.SendMessage(message);
            PutMessageInList(message);
            AddMessageToDatabase(message);
        }

        /// <summary>
        /// Put the self written Message into the Database
        /// </summary>
        /// <param name="message"></param>
        private static void AddMessageToDatabase(string message) {
            // Database
            StoreIndatabase(new Models.Chat.Messages {
                TimeStamp = DateTime.Now,
                TwitchID = Core.AivaClient.Instance.TwitchID,
                IsUserMod = true,
                IsUserSub = false,
                Message = message,
                Username = Core.AivaClient.Instance.Username
            });
        }

        /// <summary>
        /// Put the self written Message into the Messageslist
        /// </summary>
        /// <param name="message"></param>
        private void PutMessageInList(string message) {
            Messages.Add(
                            new Models.Chat.Messages {
                                IsUserMod = true,
                                IsUserSub = false,
                                TimeStamp = DateTime.Now,
                                Message = message,
                                TwitchID = Core.AivaClient.Instance.TwitchID,
                                Username = Core.AivaClient.Instance.Username
                            });
        }
    }
}
