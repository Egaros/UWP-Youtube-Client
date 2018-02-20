﻿using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Playback;
using Windows.Networking.BackgroundTransfer;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using YoutubeExplode;
using YTApp.Classes;
using YTApp.Classes.DataTypes;
using YTApp.UserControls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace YTApp.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class VideoPage : Page
    {
        YoutubeExplode.Models.MediaStreams.MediaStreamInfoSet videoStreams;

        Channel channel;

        PlaylistDataType relatedVideos = new PlaylistDataType();

        public VideoPage()
        {
            this.InitializeComponent();

            //Keep the page in memory so we don't have to reload it everytime
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ConnectedAnimation imageAnimation = ConnectedAnimationService.GetForCurrentView().GetAnimation("videoThumb");

            if (imageAnimation != null)
            {
                imageAnimation.TryStart(MediaElementContainer);
            }

            Constants.MainPageRef.contentFrame.Navigated += ContentFrame_Navigated;
            SystemNavigationManager.GetForCurrentView().BackRequested += VideoPage_BackRequested;

            //Get the video data and play it
            StartVideo(Constants.activeVideoID);

            //Update likes/dislikes
            LikeDislikeControl.UpdateData();

            Constants.MainPageRef.SwitchToFullSize += CustomMediaTransportControls_SwitchedToFullSize;
        }

        private void VideoPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            ChangePlayerSize(false);
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            ChangePlayerSize(false);
        }

        #region Methods
        public async void StartVideo(string ID)
        {
            //Make the player cover the entire frame
            ChangePlayerSize(true);

            try
            {
                viewer.Source = Constants.activeVideoID;
            }
            catch
            {
                InAppNotif.Show();
                return;
            }

            var service = await YoutubeItemMethodsStatic.GetServiceAsync();

            var getVideoInfo = service.Videos.List("snippet, statistics, contentDetails");
            getVideoInfo.Id = ID;
            var videoList = await getVideoInfo.ExecuteAsync();

            //Checks to see if video exists and sets the video variable if it does or returns if it doesn't
            try { Constants.activeVideo = videoList.Items[0]; }
            catch
            {
                InAppNotif.Show();
                return;
            }

            //Channel Info
            await Task.Run(() =>
            {
                var getChannelInfo = service.Channels.List("snippet");
                getChannelInfo.Id = Constants.activeVideo.Snippet.ChannelId;
                var channelInfo = getChannelInfo.Execute();
                channel = channelInfo.Items[0];
            });

            UpdatePageInfo(service);

            UpdateRelatedVideos(service);

        }

        private async void InAppNotifButton_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://youtu.be/" + Constants.activeVideoID));
            InAppNotif.Dismiss();
        }

        private void InAppNotifButton2_Click(object sender, RoutedEventArgs e)
        {
            InAppNotif.Dismiss();
        }

        public void UpdatePageInfo(YouTubeService service)
        {
            var methods = new YoutubeItemMethods();

            Title.Text = Constants.activeVideo.Snippet.Title;
            Views.Text = string.Format("{0:#,###0.#}", Constants.activeVideo.Statistics.ViewCount) + " Views";

            ChannelTitle.Text = channel.Snippet.Title;
            DatePosted.Text = Constants.activeVideo.Snippet.PublishedAt.Value.ToString("MMMM d, yyyy");
            Description.Text = Constants.activeVideo.Snippet.Description;
            DescriptionShowMore.Visibility = Visibility.Visible;
            var image = new BitmapImage(new Uri(channel.Snippet.Thumbnails.High.Url));
            var imageBrush = new ImageBrush{ ImageSource = image };
            ChannelProfileIcon.Fill = imageBrush;
        }

        public async void UpdateRelatedVideos(YouTubeService service)
        {
            System.Collections.ObjectModel.ObservableCollection<YoutubeItemDataType> relatedVideosList = new System.Collections.ObjectModel.ObservableCollection<YoutubeItemDataType>();
            await Task.Run(() =>
            {
                var getRelatedVideos = service.Search.List("snippet");
                getRelatedVideos.RelatedToVideoId = Constants.activeVideoID;
                getRelatedVideos.MaxResults = 15;
                getRelatedVideos.Type = "video";
                var relatedVideosResponse = getRelatedVideos.Execute();

                var methods = new YoutubeItemMethods();
                foreach (SearchResult video in relatedVideosResponse.Items)
                {
                    relatedVideosList.Add(methods.VideoToYoutubeItem(video));
                }
                methods.FillInViews(relatedVideosList, service);
            });
            relatedVideos.Items = relatedVideosList;
        }

        private void YoutubeItemsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = (YoutubeItemDataType)e.ClickedItem;
            Constants.MainPageRef.StartVideo(item.Id);
        }

        //AChangePlayerSize takes a bool allowing you to set it to fullscreen (true) or to a small view (false)
        public void ChangePlayerSize(bool MakeFullScreen)
        {
            if (!MakeFullScreen)
            {
                viewer.transportControls.Visibility = Visibility.Collapsed;

                Scrollviewer.ChangeView(0, 0, 1, true);
                Scrollviewer.VerticalScrollMode = ScrollMode.Disabled;
                Scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                Frame.HorizontalAlignment = HorizontalAlignment.Right;
                Frame.VerticalAlignment = VerticalAlignment.Bottom;
                Frame.Width = 640;
                Frame.Height = 360;

                //Saves the current Media Player height
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values["MediaViewerHeight"] = MediaRow.Height.Value;

                MediaRow.Height = new GridLength(360);
            }
            else
            {
                viewer.transportControls.Visibility = Visibility.Visible;

                Scrollviewer.VerticalScrollMode = ScrollMode.Auto;
                Scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                Frame.HorizontalAlignment = HorizontalAlignment.Stretch;
                Frame.VerticalAlignment = VerticalAlignment.Stretch;
                Frame.Width = Double.NaN;
                Frame.Height = Double.NaN;

                //Set the media viewer to the previous height or to the default if a custom height is not found
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (localSettings.Values["MediaViewerHeight"] != null && (double)localSettings.Values["MediaViewerHeight"] > 360)
                    MediaRow.Height = new GridLength(Convert.ToDouble(localSettings.Values["MediaViewerHeight"]));
                else
                    MediaRow.Height = new GridLength(600);
            }
        }

        #endregion

        #region Events

        private void MinimizeMediaElement_Click(object sender, RoutedEventArgs e)
        {
            if (MediaRow.Height.Value == 360) { ChangePlayerSize(true); }
            else { ChangePlayerSize(false); }
        }

        private void CloseMediaElement_Click(object sender, RoutedEventArgs e)
        {
            viewer.StopVideo();
            Frame.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region MediaElementButton Management
        private void Viewer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            FadeIn.Begin();
        }

        private void Viewer_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            FadeOut.Begin();
        }
        #endregion

        private void CustomMediaTransportControls_SwitchedToCompact(object sender, EventArgs e)
        {
            Constants.MainPageRef.viewer.Source = new Uri(videoStreams.Muxed[0].Url);
            Constants.MainPageRef.viewer.Visibility = Visibility.Visible;
            var coreTitleBar = Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
        }

        private void CustomMediaTransportControls_SwitchedToFullSize(object sender, EventArgs e)
        {
            Constants.MainPageRef.viewer.Visibility = Visibility.Collapsed;

            //Reset title to bar to it's normal state
            var coreTitleBar = Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;

            //Set the position of this video page's viewer to the position of the compact view's one
            viewer.timelineController.Position = Constants.MainPageRef.viewer.Position;
            viewer.timelineController.Start();
        }

        private void OpenChannel(object sender, TappedRoutedEventArgs e)
        {
            Constants.activeChannelID = channel.Id;
            Constants.MainPageRef.contentFrame.Navigate(typeof(ChannelPage));
        }

        private void ChannelProfileIcon_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 1);
        }

        private void ChannelProfileIcon_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 2);
        }

        private void DescriptionShowMore_Click(object sender, RoutedEventArgs e)
        {
            if ((string)DescriptionShowMore.Content == "Show less")
            {
                Description.MaxLines = 6;
                DescriptionShowMore.Content = "Show more";
            }
            else
            {
                Description.MaxLines = 400;
                DescriptionShowMore.Content = "Show less";
            }
        }

        private void viewer_EnteringFullscreen(object sender, EventArgs e)
        {
            Constants.MainPageRef.Toolbar.Visibility = Visibility.Collapsed;
            MediaRow.Height = new GridLength();
            Scrollviewer.ChangeView(0, 0, 1, true);
            Scrollviewer.VerticalScrollMode = ScrollMode.Disabled;
            Scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }

        private void viewer_ExitingFullscren(object sender, EventArgs e)
        {
            Constants.MainPageRef.Toolbar.Visibility = Visibility.Visible;

            Scrollviewer.VerticalScrollMode = ScrollMode.Enabled;
            Scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values["MediaViewerHeight"] != null && (double)localSettings.Values["MediaViewerHeight"] > 360)
                MediaRow.Height = new GridLength(Convert.ToDouble(localSettings.Values["MediaViewerHeight"]));
            else
                MediaRow.Height = new GridLength(600);
        }
    }
}
