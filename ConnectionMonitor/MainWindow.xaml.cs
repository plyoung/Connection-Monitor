using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.IO;
using System.Diagnostics;
using System.Windows.Threading;
using System.Net;
using System.Net.NetworkInformation;

namespace ConnectionMonitor
{
	public partial class MainWindow : Window
	{
		private bool isRunning = false;
		private bool wasOnline = false;
		private int interval;
		private string addy1;
		private string addy2;
		private string addy3;
		private IPAddress ip1;
		private IPAddress ip2;
		private IPAddress ip3;
		private DispatcherTimer timer;
		private System.Windows.Forms.NotifyIcon ni;
		private Ping ping;

		public MainWindow()
		{
			InitializeComponent();

			ping = new Ping();

			ni = new System.Windows.Forms.NotifyIcon();
			ni.Icon = System.Drawing.Icon.FromHandle(Properties.Resources.plug.GetHicon());
			ni.Visible = true;
			ni.DoubleClick += delegate (object sender, EventArgs args)
			{
				this.Show();
				this.WindowState = WindowState.Normal;
			};

			intervalTextBox.Text = Properties.Settings.Default.interval;
			address1TextBox.Text = Properties.Settings.Default.addy1;
			address2TextBox.Text = Properties.Settings.Default.addy2;
			address3TextBox.Text = Properties.Settings.Default.addy3;
			statusLabel.Content = "";
			address1ResLabel.Content = "";
			address2ResLabel.Content = "";
			address3ResLabel.Content = "";

			timer = new DispatcherTimer();
			timer.Tick += new EventHandler(TimedEvent);
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			timer.Stop();
			ni.Visible = false;
			ni.Dispose();
			ping.Dispose();
		}

		protected override void OnStateChanged(EventArgs e)
		{
			if (WindowState == System.Windows.WindowState.Minimized) this.Hide();
			base.OnStateChanged(e);
		}

		private void ValidateNumber(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}

		private void openLogButton_Click(object sender, RoutedEventArgs e)
		{
			Process.Start(GetLogPath());
		}

		private void clearLogButton_Click(object sender, RoutedEventArgs e)
		{
			logListBox.Items.Clear();
			try
			{
				string fn = GetLogPath();
				File.Delete(fn);
				File.WriteAllText(fn, "");
			}
			catch { }
		}

		private void playButton_Click(object sender, RoutedEventArgs e)
		{
			if (isRunning)
			{
				wasOnline = false;
				isRunning = false;
				playButton.Content = "";

				ni.Icon = System.Drawing.Icon.FromHandle(Properties.Resources.plug.GetHicon());
				Uri iconUri = new Uri("pack://application:,,,/res/plug.png", UriKind.RelativeOrAbsolute);
				this.Icon = BitmapFrame.Create(iconUri);

				intervalTextBox.IsEnabled = true;
				address1TextBox.IsEnabled = true;
				address2TextBox.IsEnabled = true;
				address3TextBox.IsEnabled = true;
				statusLabel.Content = "";
				address1ResLabel.Content = "";
				address2ResLabel.Content = "";
				address3ResLabel.Content = "";

				UpdateLog("Stopped");

				timer.Stop();
			}
			else
			{
				isRunning = true;
				playButton.Content = "";

				intervalTextBox.IsEnabled = false;
				address1TextBox.IsEnabled = false;
				address2TextBox.IsEnabled = false;
				address3TextBox.IsEnabled = false;
				statusLabel.Content = "";
				address1ResLabel.Content = "";
				address2ResLabel.Content = "";
				address3ResLabel.Content = "";

				if (false == int.TryParse(intervalTextBox.Text, out interval)) interval = 60;

				addy1 = address1TextBox.Text;
				addy2 = address2TextBox.Text;
				addy3 = address3TextBox.Text;

				ip1 = GetIP(addy1);
				ip2 = GetIP(addy2);
				ip3 = GetIP(addy3);

				Properties.Settings.Default.interval = interval.ToString();
				Properties.Settings.Default.addy1 = addy1;
				Properties.Settings.Default.addy2 = addy2;
				Properties.Settings.Default.addy3 = addy3;
				Properties.Settings.Default.Save();

				UpdateLog("Started");

				TimedEvent(null, null);
				timer.Interval = new TimeSpan(0, 0, interval);
				timer.Start();
			}
		}

		private void UpdateLog(string entry)
		{
			entry = DateTime.Now + " - " + entry;

			try
			{
				File.AppendAllLines(GetLogPath(), new string[] { entry });
			}
			catch { }

			logListBox.Items.Add(entry);

			ListBoxAutomationPeer svAutomation = (ListBoxAutomationPeer)ScrollViewerAutomationPeer.CreatePeerForElement(logListBox);
			IScrollProvider scrollInterface = (IScrollProvider)svAutomation.GetPattern(PatternInterface.Scroll);
			System.Windows.Automation.ScrollAmount scrollVertical = System.Windows.Automation.ScrollAmount.LargeIncrement;
			System.Windows.Automation.ScrollAmount scrollHorizontal = System.Windows.Automation.ScrollAmount.NoAmount;
			if (scrollInterface.VerticallyScrollable) scrollInterface.Scroll(scrollHorizontal, scrollVertical);
		}

		private string GetLogPath()
		{
			string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			path += "\\PLYoung\\ConnectionMonitor";

			try
			{
				if (!Directory.Exists(path)) Directory.CreateDirectory(path);
			}
			catch { }

			return path + "\\log.txt";
		}

		private IPAddress GetIP(string addy)
		{
			if (string.IsNullOrEmpty(addy)) return null;
			try
			{
				IPAddress[] ips = Dns.GetHostAddresses(addy);
				if (ips.Length > 0) return ips[0];
			}
			catch { }
			return null;
		}

		private void TimedEvent(object sender, EventArgs e)
		{
			bool online = false;
			bool oneOrMoreError = false;
			long ms = 0;
			string res = "";

			if (ip1 != null)
			{
				if (DoPing(ip1, out ms))
				{
					online = true;
					address1ResLabel.Content = ms + " ms";
					res += "#";
				}
				else
				{
					oneOrMoreError = true;
					address1ResLabel.Content = "-error-";
					res += "x";
				}
			}

			if (ip2 != null)
			{
				if (DoPing(ip2, out ms))
				{
					online = true;
					address2ResLabel.Content = ms + " ms";
					res += "#";
				}
				else
				{
					oneOrMoreError = true;
					address2ResLabel.Content = "-error-";
					res += "x";
				}
			}

			if (ip3 != null)
			{
				if (DoPing(ip3, out ms))
				{
					online = true;
					address3ResLabel.Content = ms + " ms";
					res += "#";
				}
				else
				{
					oneOrMoreError = true;
					address3ResLabel.Content = "-error-";
					res += "x";
				}
			}

			if (online)
			{
				if (!wasOnline || oneOrMoreError)
				{
					wasOnline = true; // only update log if status changed
					UpdateLog("[" + res + "] Online");

					statusLabel.Content = "";
					ni.Icon = System.Drawing.Icon.FromHandle(Properties.Resources.signal.GetHicon());
					Uri iconUri = new Uri("pack://application:,,,/res/signal.png", UriKind.RelativeOrAbsolute);
					this.Icon = BitmapFrame.Create(iconUri);
				}
			}
			else
			{
				if (wasOnline || oneOrMoreError)
				{
					wasOnline = false; // only update log if status changed
					UpdateLog("[!] Connection Lost");

					statusLabel.Content = "";

					ni.Icon = System.Drawing.Icon.FromHandle(Properties.Resources.error.GetHicon());
					Uri iconUri = new Uri("pack://application:,,,/res/error.png", UriKind.RelativeOrAbsolute);
					this.Icon = BitmapFrame.Create(iconUri);
				}
			}
		}

		private bool DoPing(IPAddress ip, out long ms)
		{
			ms = 0;
			try
			{
				PingReply reply = ping.Send(ip);
				if (reply.Status == IPStatus.Success)
				{
					ms = reply.RoundtripTime;
					return true;
				}
			}
			catch { }

			return false;
		}
	}
}
