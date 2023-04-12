// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using ATL;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ChapEdit
{
	/// <summary>
	/// THe one, the only WinUI 3 window for Chapter Editor.
	/// Not the most exotic application architecture around!
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		private ObservableCollection<FormattedAudioChapter> Chapters;
		private AudioTagParser Audio;
		private bool HasBorkedTimestamp;
		private AppWindow appWindow;

		public MainWindow() {
			this.InitializeComponent();
			this.ExtendsContentIntoTitleBar = true;
			SetTitleBar(AppTitleBar);

			this.Chapters = new ObservableCollection<FormattedAudioChapter>();
			this.HasBorkedTimestamp = false;

			IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this); // m_window in App.cs
			WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
			appWindow = AppWindow.GetFromWindowId(windowId);

			// Set title and icon
			appWindow.Title = "Audio Chapter Editor";
			appWindow.SetIcon(Path.Combine(Package.Current.InstalledLocation.Path, "Resources\\BarCafeChair.ico"));

			ResizeWindowToContents();
		}

		private void ResizeWindowToContents() {
			var size = new Windows.Graphics.SizeInt32();
			if (Chapters.Count == 0) {
				size.Width = 500;
				size.Height = 500;
			} else {
				size.Width = 1000;
				size.Height = 1000;
			}
			appWindow.Resize(size);
		}

		/// <summary>
		/// The ATL library uses a much more expansive object called ChapterInfo
		/// </summary>
		private ChapterInfo[] ConvertChapterFormat() {
			var atlChapters = new List<ChapterInfo>();
			Chapters.ToList().ForEach(c => atlChapters.Add(c.GetChapterInfo()));
			return atlChapters.ToArray();
		}

		private async void PickAFileButton_Click(object sender, RoutedEventArgs e) {
			// Create a file picker
			var openPicker = new Windows.Storage.Pickers.FileOpenPicker();

			// Retrieve the window handle (HWND) of the current WinUI 3 window.
			var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

			// Initialize the file picker with the window handle (HWND).
			WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

			// Set options for your file picker
			openPicker.ViewMode = PickerViewMode.Thumbnail;
			openPicker.FileTypeFilter.Add(".mp3");
			openPicker.FileTypeFilter.Add(".m4a");

			// Open the picker for the user to pick a file
			var file = await openPicker.PickSingleFileAsync();

			// Album art updating
			if (file != null) {
				PickAFileButton.Content = file.Name;
				this.Audio = new AudioTagParser(file);
				Chapters.Clear();
				var chaps = Audio.GetChapters().ToList();
				FileInfoPanel.Visibility = Visibility.Visible;
				AddButton.Visibility = Visibility.Visible;
				if (Audio.GetAlbumArt() != null) {
					var bitmapImage = new BitmapImage();
					bitmapImage.CreateOptions = BitmapCreateOptions.None;
					using (var stream = new InMemoryRandomAccessStream()) {
						using (var writer = new DataWriter(stream)) {
							writer.WriteBytes(Audio.GetAlbumArt());
							await writer.StoreAsync();
							await writer.FlushAsync();
							writer.DetachStream();
						}
						stream.Seek(0);
						await bitmapImage.SetSourceAsync(stream);
					}
					AlbumArt.Source = bitmapImage;
					AlbumArtViewPane.IsPaneOpen = true;
				}
				else {
					AlbumArt.Source = null;
					AlbumArtViewPane.IsPaneOpen = false;
				}

				// Chapter info updating - the good stuff
				if (chaps.Any()) {
					chaps.ForEach(c => Chapters.Add(
						new FormattedAudioChapter(c.Title,
						AudioTagParser.FormatChapterTimeFromMillis(c.StartTime))));
					FileInfoPanel.Text = Audio.GetFileInfo();
				} else {
					Chapters.Add(new FormattedAudioChapter("< Chapter title here>", "00:00:00.000"));
					FileInfoPanel.Text = "No chapters found in the selected audio file.";
				}
			}

			ResizeWindowToContents();
		}

		private void TimestampFocused(object sender, RoutedEventArgs e) {
			CheckSaveButton();
			var textBox = (TextBox)sender;
			textBox.Select(0, textBox.Text.Length);
		}

		private void TimestampLeft(object sender, RoutedEventArgs e) {
			// Get the current text of the TextBox
			var timeStr = ((TextBox)sender).Text;
			// Assess if it's a valid timestamp
			var parseResult = AudioTagParser.GetTimestampString(timeStr);

			// Move this into a method so it can be called from FormattedTimestamp
			if (parseResult.SuccessfullyParsed) {
				((TextBox)sender).Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
				((TextBox)sender).Text = $"{parseResult.TimestampResult:hh\\:mm\\:ss\\.fff}";
			} else {
				((TextBox)sender).Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 0, 0));
			}
		}

		private void CheckSaveButton() {
			SaveButton.IsEnabled = !Chapters.ToList().Any(c => !AudioTagParser.GetTimestampString(c.Timestamp).SuccessfullyParsed);
		}

		private void AddNewChapter(object sender, RoutedEventArgs e) {
			Chapters.Add(new FormattedAudioChapter());
			SaveButton.Visibility = Visibility.Visible;
		}

		private void Save(object sender, RoutedEventArgs e) {
			Audio.UpdateChapters(ConvertChapterFormat());
		}
	}
}