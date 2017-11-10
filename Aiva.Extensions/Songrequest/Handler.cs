﻿using Aiva.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using TwitchLib.Events.Client;

namespace Aiva.Extensions.Songrequest {

    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class Handler {

        #region Models

        public bool IsStarted { get; set; }
        public bool IsPlaying { get; set; }

        public Models.Songrequest.AddModel Properties { get; set; }
        public ObservableCollection<Models.Songrequest.SongModel> SongList { get; set; }
        public Models.Songrequest.SongModel SelectedSong { get; set; }

        public Player Player { get; set; }

        private Core.DatabaseHandlers.Currency _currencyDatabaseHandler;

        #endregion Models

        #region Constructor

        public Handler() {
            _currencyDatabaseHandler = new Core.DatabaseHandlers.Currency();
            SongList = new ObservableCollection<Models.Songrequest.SongModel>();
            Player = new Player();
            Core.AivaClient.Instance.AivaTwitchClient.OnChatCommandReceived += AddSongCommandReceived;
            IsStarted = true;
        }

        #endregion Constructor

        #region Functions

        /// <summary>
        /// Stop the registration
        /// </summary>
        public void StopRegistration()
            => Core.AivaClient.Instance.AivaTwitchClient.OnChatCommandReceived -= AddSongCommandReceived;

        /// <summary>
        /// Plays the selected song
        /// </summary>
        public void PlaySelectedSong() {
            var song = SelectedSong;
            SongList.Remove(SelectedSong);
            SongList.Insert(0, song);
            Player.ChangeSong(song);
        }

        /// <summary>
        /// Starts the next song
        /// </summary>
        public void NextSong() {
            SongList.RemoveAt(0);

            if (SongList.Any()) {
                Player.CurrentSong = SongList[0];
            }
        }

        /// <summary>
        /// Fires when a command was received
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddSongCommandReceived(object sender, OnChatCommandReceivedArgs e) {
            if (String.Compare(Properties.Command.TrimStart('!'), e.Command.CommandText, true) == 0) {
                // price
                if (Properties.IsCostEnabled) {
                    if (!_currencyDatabaseHandler.HasUserEnoughCurrency(e.Command.ChatMessage.UserId, Properties.Cost)) {
                        return;
                    }
                }

                // multi adding
                if (Properties.BlockMultiSong) {
                    var userAlreadyInSonglist = SongList.SingleOrDefault(u => String.Compare(u.RequesterID, e.Command.ChatMessage.UserId, true) == 0);

                    if (userAlreadyInSonglist != null) {
                        return;
                    }
                }

                // permissions
                if (Properties.JoinPermission != Enums.JoinPermission.Everyone) {
                    if (Properties.JoinPermission == Enums.JoinPermission.Subscriber) {
                        if (!e.Command.ChatMessage.IsSubscriber) {
                            return;
                        }
                    }

                    if (Properties.JoinPermission == Enums.JoinPermission.Moderation) {
                        if (!e.Command.ChatMessage.IsModerator) {
                            return;
                        }
                    }
                }

                // follower
                if (Properties.BeFollower) {
                    var followerCheck = await AivaClient.Instance.TwitchApi.Users.v5.UserFollowsChannelAsync(e.Command.ChatMessage.UserId, Core.AivaClient.Instance.ChannelID);

                    if (!followerCheck) {
                        return;
                    }
                }

                // add the song to the list
                AddSong(e.Command.ArgumentsAsString, e.Command.ChatMessage.DisplayName, e.Command.ChatMessage.UserId);
            }
        }

        /// <summary>
        /// Add a song to the database
        /// </summary>
        /// <param name="args"></param>
        /// <param name="displayName"></param>
        /// <param name="userID"></param>
        /// <param name="autoStart"></param>
        private void AddSong(string args, string displayName, string userID, bool autoStart = false) {
            // check song on youtube
            var song = new Song(args);
            if (!song.FoundVideo) {
                return;
            }

            // create model to add to songlist
            var songlistSongModel = new Models.Songrequest.SongModel {
                Url = song.Url,
                Length = song.Duration.ToString(),
                Requester = displayName,
                RequesterID = userID,
                Title = song.Title,
                VideoID = song.VideoID
            };

            // add to songlist
            Application.Current.Dispatcher.Invoke(() => {
                SongList.Add(songlistSongModel);
            });

            // dont call to play the video twice
            if (!autoStart) {
                // if first song -> play song if auto start is selected
                if (Properties.AutoStart && !IsPlaying) {
                    Player.ChangeSong(songlistSongModel);
                    IsPlaying = true;
                }
            }

            if (autoStart) {
                Player.ChangeSong(songlistSongModel);
                if (!IsPlaying)
                    IsPlaying = !IsPlaying;
            }
        }

        public void AddSong(string video, bool instantStart) {
            AddSong(video, Core.AivaClient.Instance.Username, Core.AivaClient.Instance.TwitchID, instantStart);
        }

        /// <summary>
        /// Checks if the user has enough currency
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private bool UserHasEnoughCurrency(string id) {
            var currency = _currencyDatabaseHandler.GetCurrency(id);

            if (currency.HasValue) {
                if (currency.Value >= Properties.Cost) {
                    return true;
                }
            }

            return false;
        }

        #endregion Functions
    }
}