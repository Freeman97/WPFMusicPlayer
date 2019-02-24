using System;
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
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using Shell32;
using System.Windows.Threading;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Formatters.Binary;

namespace WpfApp1
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    [Serializable]
    public class Music
    {
        private string MusicUri;
        public string MusicName;
        public string MusicArtist;
        private string LyricUri;
        private string Duration;
        public Music(string Uri, string MusicName, string Artist, string LyricUri, string duration)
        {
            this.MusicUri = Uri;
            this.MusicName = MusicName;
            this.MusicArtist = Artist;
            this.LyricUri = LyricUri;
            this.Duration = duration;
        }

        public Music(string Uri):this(Uri, "", "", "", "")
        {
            // this.MusicUri = Uri;
        }

        public Music(string Uri, string MusicName):this(Uri, MusicName, "", "", "")
        {        }
        
        public Music(string Uri, string MusicName, string Artist, string duration):this(Uri, MusicName, Artist, "", duration)
        { }

        public string Uri
        {
            get
            {
                return MusicUri;   
            }
        }
        public string Lyric
        {
            get
            {
                return LyricUri;
            }
            set
            {
                this.LyricUri = value;
            }
        }
        public string Title
        {
            get
            {
                return MusicName;
            }
        }
        public string Artist
        {
            get
            {
                return MusicArtist;
            }
        }
        public string Length
        {
            get
            {
                return Duration;
            }
        }
    }

    [Serializable]
    public class Playlist
    {
        public string Name;
        public List<Music> Musiclist;
        public Playlist(string name)
        {
            this.Name = name;
            this.Musiclist = new List<Music>();
        }
        public string playListName
        {
            get
            {
                return Name;
            }
            set
            {
                Name = value;
            }
        }
    }

    public class LyricLine
    {
        public string Line;
        public TextBlock Element;
        public LyricLine(string Line)
        {
            this.Line = Line;
        }
    }

    public class Lyric
    {
        // Dictionary相当于C++ STL中的Map容器
        public Dictionary<double, LyricLine> timeWord = new Dictionary<double, LyricLine>();
        public Lyric(string uri)
        {
            using (FileStream fs = new FileStream(uri, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                string line;
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                {
                    while((line = sr.ReadLine()) != null)
                    {
                        try
                        {
                            Regex rtime = new Regex(@"\[([0-9.:]*)\]", RegexOptions.Compiled);
                            MatchCollection mtime = rtime.Matches(line);
                            Regex rword = new Regex(@".*\](.*)", RegexOptions.Compiled);
                            Match mword = rword.Match(line);
                            string word = mword.Groups[1].Value;
                            foreach (Match item in mtime)
                            {
                                double time = TimeSpan.Parse("00:" + item.Groups[1].Value).TotalSeconds;
                                timeWord.Add(time, new LyricLine(word));
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
        }
    }

    public partial class MainWindow : Window
    {
        Music currentMusic = null;
        List<Playlist> allList;
        Playlist currentList;
        Playlist currentShowingList;
        Playlist currentRandomList;
        int deletedIndex = 1;
        DispatcherTimer timer = new DispatcherTimer();
        LyricLine currentFocusLyric;
        Lyric currentLyric = null;
        bool ifPlaying = false;
        enum playMode
        {
            Sequential = 0,
            Listloop = 1,
            Random = 2,
            SingleLoop = 3
        }
        int currentPlayMode = 0;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void p_MediaEnded(object sender, RoutedEventArgs e)
        {
            lrc_items.Children.Clear();
            p.Stop();
            currentLyric = null;
            ifPlaying = false;
            play.Content = "播放";
            progressEnd.Content = "00:00";
            switch((playMode)currentPlayMode)
            {
                case playMode.Sequential:
                    {
                        if(currentList.Musiclist.LastIndexOf(currentMusic) == currentList.Musiclist.Count - 1)
                        {
                            NowPlaying.Content = "";
                            return;
                        }
                        currentMusic = getNext(currentList);
                        p.Source = new Uri(currentMusic.Uri);
                        playCurrentMusic();
                        break;
                    }
                case playMode.Listloop:
                    {
                        currentMusic = getNext(currentList);
                        p.Source = new Uri(currentMusic.Uri);
                        playCurrentMusic();
                        break;
                    }
                case playMode.Random:
                    {
                        currentMusic = getNext(currentRandomList);
                        p.Source = new Uri(currentMusic.Uri);
                        playCurrentMusic();
                        break;
                    }
                case playMode.SingleLoop:
                    {
                        playCurrentMusic();
                        break;
                    }
            }
        }

        private void orderfile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog1 = new Microsoft.Win32.OpenFileDialog();
            if (openFileDialog1.ShowDialog().Value)
            {
                ShellClass sh = new ShellClass();
                Folder dir = sh.NameSpace(System.IO.Path.GetDirectoryName(openFileDialog1.FileName));
                FolderItem item = dir.ParseName(System.IO.Path.GetFileName(openFileDialog1.FileName));
                string title = dir.GetDetailsOf(item, 21);
                string artist = dir.GetDetailsOf(item, 20);
                string duration = dir.GetDetailsOf(item, 27);
                Music m = new Music(System.IO.Path.GetFullPath(openFileDialog1.FileName), title, artist, duration);
                currentShowingList.Musiclist.Add(m);
                playlist.Items.Refresh();
            }
        }
        
        private void play_Click(object sender, RoutedEventArgs e)
        {
            if(currentMusic == null && currentList.Musiclist.Count >= 1)
            {
                currentMusic = currentList.Musiclist[0];
                p.Source = new Uri(currentMusic.Uri);
            }
            if(currentMusic == null && currentList.Musiclist.Count <= 0)
            {
                System.Windows.MessageBox.Show("请先添加歌曲到播放列表!", "错误", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            if(ifPlaying)
            {
                p.Pause();
                play.Content = "播放";
                ifPlaying = false;
            }
            else
            {
                ifPlaying = true;
                play.Content = "暂停";
                playCurrentMusic();
            }
        }
        
        // initialize
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 从文件中读取出播放列表的信息
            try
            {
                FileStream fs = new FileStream(@"playlistinfo.dat", FileMode.Open);
                BinaryFormatter bf = new BinaryFormatter();
                allList = bf.Deserialize(fs) as List<Playlist>;
                currentList = allList[0];
                currentShowingList = currentList;
                fs.Close();
            }
            catch(FileNotFoundException exp)
            {
                allList = new List<Playlist>();
                currentList = new Playlist("默认播放列表");
                allList.Add(currentList);
                currentShowingList = currentList;
            }
            // 初始化其它状态变量
            playlist.ItemsSource = currentShowingList.Musiclist;
            allPlayList.ItemsSource = allList;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            timer.Tick += new EventHandler(timerTicked);
            p.Volume = 1;
            volumeSlider.Maximum = 1;
            volumeSlider.Minimum = 0;
            volumeSlider.Value = 1;
        }

        private void timerTicked(object sender, EventArgs e)
        {
            progressSlider.Value = p.Position.TotalSeconds;
            string minute = p.Position.Minutes.ToString();
            if(p.Position.Minutes < 10)
            {
                minute = "0" + minute;
            }
            string second = p.Position.Seconds.ToString();
            if(p.Position.Seconds < 10)
            {
                second = "0" + second;
            }
            string currentPositionText = minute + ":" + second;
            progressStart.Content = currentPositionText;
            if(currentLyric != null)
            {
                rollLyric(p.Position.TotalSeconds);
            }
        }

        private Music getPrivious(Playlist pl)
        {
            int index = pl.Musiclist.LastIndexOf(currentMusic);
            if(index == 0)
            {
                index = pl.Musiclist.Count;
            }
            else if(index == -1)
            {
                index = deletedIndex;
            }
            return pl.Musiclist[index - 1];
        }

        private Music getNext(Playlist pl)
        {
            int index = pl.Musiclist.LastIndexOf(currentMusic);
            if (index == pl.Musiclist.Count - 1)
            {
                index = -1;
            }
            else if (index == -1)
            {
                index = deletedIndex;
            }
            return pl.Musiclist[index + 1];
        }

        private void playCurrentMusic()
        {
            progressEnd.Content = currentMusic.Length;
            init(currentMusic.Lyric);
            NowPlaying.Content = "正在播放: " + currentMusic.Title;
            p.Play();
        }

        private void p_MediaOpened(object sender, RoutedEventArgs e)
        {
            ifPlaying = true;
            play.Content = "暂停";
            progressSlider.Maximum = p.NaturalDuration.TimeSpan.TotalSeconds;
            timer.Start();
        }

        private void progressSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            p.Position = TimeSpan.FromSeconds(progressSlider.Value);
            string minute = p.Position.Minutes.ToString();
            string second = p.Position.Seconds.ToString();
            string currentPositionText = minute + ":" + second;
            progressStart.Content = currentPositionText;
            if (currentLyric != null)
            {
                rollLyric(p.Position.TotalSeconds);
            }
            timer.Start();
        }

        private void progressSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            timer.Stop();
        }

        private void changePlayMode_Click(object sender, RoutedEventArgs e)
        {
            if(currentPlayMode == 3)
            {
                currentPlayMode = 0;
            }
            else
            {
                currentPlayMode += 1;
            }
            switch((playMode)currentPlayMode)
            {
                case playMode.Listloop:
                    {
                        changePlayMode.Content = "列表循环";
                        break;
                    }
                case playMode.Random:
                    {
                        changePlayMode.Content = "随机播放";
                        playListRandomize();
                        break;
                    }
                case playMode.Sequential:
                    {
                        changePlayMode.Content = "顺序播放";
                        break;
                    }
                case playMode.SingleLoop:
                    {
                        changePlayMode.Content = "单曲循环";
                        break;
                    }
            }
        }
        
        private void playListRandomize()
        {
            if(currentList.Musiclist.Count <= 0)
            {
                return;
            }
            // 随机打乱当前播放列表的顺序
            int randomCount = currentList.Musiclist.Count;
            currentRandomList = new Playlist("randomlist");
            currentRandomList.Musiclist.Capacity = randomCount;
            Random r = new Random();
            // 生成一个随机排列数组
            int[] randomArray = new int[randomCount];
            for(int i = 0; i < randomArray.Length; i++)
            {
                randomArray[i] = i;
            }
            while(randomCount-- >= 0)
            {
                int i1 = r.Next(currentList.Musiclist.Count);
                int i2 = r.Next(currentList.Musiclist.Count);
                int temp = randomArray[i1];
                randomArray[i1] = randomArray[i2];
                randomArray[i2] = temp;
            }
            // 用此随机数组打乱当前播放列表的顺序
            for(int i = 0; i < randomArray.Length; i++)
            {
                currentRandomList.Musiclist.Add(currentList.Musiclist[randomArray[i]]);
            }
        }

        private void addLyric_Click(object sender, RoutedEventArgs e)
        {
            Music temp = playlist.SelectedItem as Music;
            Console.WriteLine(temp.Uri);
            Microsoft.Win32.OpenFileDialog openFileDialog1 = new Microsoft.Win32.OpenFileDialog(); 
            if (openFileDialog1.ShowDialog().Value)
            {
                string lyricUri = System.IO.Path.GetFullPath(openFileDialog1.FileName);
                temp.Lyric = lyricUri;
            }
        }

        // 歌词滚动相关方法
        public void init(string lyricUri)
        {
            lrc_items.Children.Clear();
            if(lyricUri == "")
            {
                currentLyric = null;
                return;
            }
            currentLyric = new Lyric(lyricUri);
            foreach (KeyValuePair<double, LyricLine> kvp in currentLyric.timeWord)
            {
                TextBlock temp = new TextBlock();
                temp.Text = kvp.Value.Line;
                kvp.Value.Element = temp;
                lrc_items.Children.Add(temp);
            }
        }

        public void rollLyric(double position)
        {
            IEnumerable<KeyValuePair<double, LyricLine>> l = currentLyric.timeWord.Where(m => position >= m.Key);
            if(l.Count() > 0)
            {
                if(currentFocusLyric != null)
                {
                    currentFocusLyric.Element.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                }
                currentFocusLyric = l.Last().Value;
                currentFocusLyric.Element.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 255));
                // 计算让该歌词滑动到scrollview需要滑动的距离
                GeneralTransform gf = currentFocusLyric.Element.TransformToVisual(lrc_items);
                Point p = gf.Transform(new Point(0, 0));
                double offset = p.Y - lrc_scrollViewer.ActualHeight / 2 + 10;
                lrc_scrollViewer.ScrollToVerticalOffset(offset);
            }
        }

        private void volumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            p.Volume = volumeSlider.Value;
        }

        private void playPrevious_Click(object sender, RoutedEventArgs e)
        {
            currentLyric = null;
            lrc_items.Children.Clear();
            p.Stop();
            if(currentPlayMode == 2)
            {
                currentMusic = getPrivious(currentRandomList);
            }
            else
            {
                currentMusic = getPrivious(currentList);
            }
            p.Source = new Uri(currentMusic.Uri);
            playCurrentMusic();
        }

        private void playNext_Click(object sender, RoutedEventArgs e)
        {
            currentLyric = null;
            lrc_items.Children.Clear();
            p.Stop();
            if (currentPlayMode == 2)
            {
                currentMusic = getNext(currentRandomList);
            }
            else
            {
                currentMusic = getNext(currentList);
            }
            p.Source = new Uri(currentMusic.Uri);
            playCurrentMusic();
        }

        // 打开程序期间全部的改动都会被添加到文件中
        private void Window_Closed(object sender, EventArgs e)
        {
            FileStream fs = new FileStream(@"playlistinfo.dat", FileMode.Create);
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(fs, allList);
            fs.Close();
        }

        private void removeMusic_Click(object sender, RoutedEventArgs e)
        {
            Music temp = playlist.SelectedItem as Music;
            currentShowingList.Musiclist.Remove(temp);
            playlist.Items.Refresh();
        }

        private void newPlayList_Click(object sender, RoutedEventArgs e)
        {
            Playlist newplaylist = new Playlist("新建播放列表");
            allList.Add(newplaylist);
            allPlayList.Items.Refresh();
        }

        private void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Controls.TextBox t = sender as System.Windows.Controls.TextBox;
            t.IsReadOnly = false;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox t = sender as System.Windows.Controls.TextBox;
            t.IsReadOnly = true;
        }

        private void allPlayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Playlist pl = allPlayList.SelectedItem as Playlist;
            if(pl == null)
            {
                return;
            }
            currentShowingList = pl;
            playlist.ItemsSource = pl.Musiclist;
            playlist.Items.Refresh();
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            p.Stop();
            Music m = playlist.SelectedItem as Music;
            currentMusic = m;
            Playlist pl = allPlayList.SelectedItem as Playlist;
            currentList = pl;
            if(currentPlayMode == 2)
            {
                playListRandomize();
            }
            p.Source = new Uri(currentMusic.Uri);
            playCurrentMusic();
        }

        private void removePlaylist_Click(object sender, RoutedEventArgs e)
        {
            Playlist pl = allPlayList.SelectedItem as Playlist;
            allPlayList.SelectedItems.Clear();
            playlist.ItemsSource = null;
            allList.Remove(pl);
            allPlayList.Items.Refresh();
        }
    }
}
