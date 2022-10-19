using System;
using System.Drawing;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using NAudio;
using NAudio.Codecs;
using NAudio.Dmo;
using NAudio.Wave;
using static System.Net.Mime.MediaTypeNames;
using static HexerDexer.Program;

namespace HexerDexer;
public class LoopStream : WaveStream
{
    WaveStream sourceStream;

    /// Creates a new Loop stream
    public LoopStream(WaveStream sourceStream)
    {
        this.sourceStream = sourceStream;
        this.EnableLooping = true;
    }

    /// Use this to turn looping on or off
    public bool EnableLooping { get; set; }

    /// Return source stream's wave format
    public override WaveFormat WaveFormat
    {
        get { return sourceStream.WaveFormat; }
    }

    /// LoopStream simply returns
    public override long Length
    {
        get { return sourceStream.Length; }
    }

    /// LoopStream simply passes on positioning to source stream
    public override long Position
    {
        get { return sourceStream.Position; }
        set { sourceStream.Position = value; }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            int bytesRead = sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
            if (bytesRead == 0)
            {
                if (sourceStream.Position == 0 || !EnableLooping)
                {
                    // something wrong with the source stream
                    break;
                }
                // loop
                sourceStream.Position = 0;
            }
            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }
}

public class WavePlayer
{
    WaveFileReader Reader;
    public WaveChannel32 Channel { get; set; }
    //private string FileName { get; set; }
    public WavePlayer(string FileName)
    {
        //this.FileName = FileName;
        Reader = new WaveFileReader(FileName);
        var loop = new LoopStream(Reader);
        Channel = new WaveChannel32(loop) { PadWithZeroes = false };
    }
    public void Dispose()
    {
        if (Channel != null)
        {
            Channel.Dispose();
            Reader.Dispose();
        }
    }
}

public class AudioEngine
{
    public static string[][] Tracks = new string[][] {
        new string[5] {"001-guitar.wav",        // Hex -> Dec
                       "002-bass.wav",
                       "003-tambourine.wav",
                       "004-atmpospherics.wav",
                       "005-drums.wav" },
        new string[7] {"song-001.wav",          // Dec -> Hex
                       "song-002.wav",
                       "song-003.wav",
                       "song-004.wav",
                       "song-005.wav",
                       "song-006.wav",
                       "song-007.wav" } };

    private static List<WavePlayer> Music = new List<WavePlayer>();
    private static DirectSoundOut MusicOutputDevice = new DirectSoundOut();
    public static void PlayMusic(string[] tracks)
    {
        if (Music.Count == 0)
        {
            foreach (string track in tracks)
            {
                Music.Add(new WavePlayer(track));
            }
            MixingWaveProvider32 mixer = new MixingWaveProvider32(Music.Select(c => c.Channel));
            MusicOutputDevice.Init(mixer);
            MusicOutputDevice.Play();
        }
        else
        {
            MusicOutputDevice.Play();
        }
    }
    public static void PlayMusic(string track) // overload for just one track
    {
        if (Music.Count == 0)
        {
            Music.Add(new WavePlayer(track));
            MixingWaveProvider32 mixer = new MixingWaveProvider32(Music.Select(c => c.Channel));
            MusicOutputDevice.Init(mixer);
            MusicOutputDevice.Play();
        }
        else
        {
            MusicOutputDevice.Play();
        }
    }
    public static void SetVolume(int trackID, float volume)
    {
        Music[trackID].Channel.Volume = volume;
    }
    public static void PauseMusic()
    {
        MusicOutputDevice.Stop();
    }
    public static void StopMusic()
    {
        MusicOutputDevice.Stop();
        foreach (WavePlayer track in Music)
        {
            track.Dispose();
        }
        Music.Clear();
        MusicOutputDevice.Dispose();

    }
    public static WaveOutEvent PlaySound(string audioLocation) // Sounds are one-off
    {
        WaveFileReader audioFile = new WaveFileReader(audioLocation);
        WaveOutEvent outputDevice = new WaveOutEvent();
        outputDevice.Init(audioFile);
        outputDevice.Play();
        return outputDevice; // And are manually stopped & disposed of after
    }
}

class Program
{
    public class ConsoleMessage
    {
        public string Contents { get; set; } = string.Empty;
        public int NewLines { get; set; } = 1;
        public ConsoleColor Color { get; set; } = ConsoleColor.White;
        public ConsoleColor Highlight { get; set; } = ConsoleColor.Black;
        public int XVal { get; set; } = 0;
        public int YVal { get; set; } = 0;
    }
    public class ConsoleLogs
    {
        private List<ConsoleMessage> ConsoleHistory = new List<ConsoleMessage>();
        public List<ConsoleMessage> History { get { return ConsoleHistory; } }
        public int Count { get { return ConsoleHistory.Count; } }
        public void Log(ConsoleMessage message) { ConsoleHistory.Add(message); }
        public void Clear() { ConsoleHistory.Clear(); }
        public void Remove(int x, int y)
        {
            ConsoleMessage? removeMe = new ConsoleMessage { XVal = -1 }; // fake item that can't exist, so nothing will be removed if item is not found
            foreach (ConsoleMessage log in ConsoleHistory)
            {
                if (log.YVal == y && log.XVal == x) { removeMe = log; } // if item is found remove it
            }
            ConsoleHistory.Remove(removeMe);
        }
        public void Shift(int x, int y, int distance)
        {
            foreach (ConsoleMessage log in ConsoleHistory)
            {
                if (log.YVal == y && log.XVal >= x)
                {
                    log.XVal += distance; // Move any character to the right of starting point by the distance
                }
            }
        }
    }
    static class MainConsole // special case of a ConsoleLogs(): this is the live one that is printed to the screen
    {
        private static readonly ConsoleLogs TheConsole = new ConsoleLogs();
        public static List<ConsoleMessage> History { get { return TheConsole.History; } }
        public static void Log(ConsoleMessage message)
        {
            TheConsole.Log(message);
        }
        public static void Remove(ConsoleMessage message)
        {
            TheConsole.Remove(message.XVal, message.YVal);
        }
        public static void Remove(int x, int y)
        {
            TheConsole.Remove(x, y);
        }
        public static void UpdateSpeaker(string oldSpeaker, string newSpeaker)
        {
            foreach (ConsoleMessage log in History)
            {
                if (log.Contents == oldSpeaker && log.XVal == 0)
                {
                    //    |->
                    // You: hello
                    // 012345->
                    // Hippo: hello
                    // 01234567->

                    int msgShift = newSpeaker.Length - oldSpeaker.Length;

                    TheConsole.Shift(oldSpeaker.Length, log.YVal, msgShift); // move message contents to align
                    log.Contents = newSpeaker;
                }
            }
        }
        public static void Clear()
        {
            TheConsole.Clear();
            Console.Clear();
        }
        public static void Refresh(ConsoleLogs? extraMessages = null)
        {
            Console.Clear();

            List<ConsoleMessage> messages = History.ToList();

            if (extraMessages != null)
            {
                foreach (ConsoleMessage message in extraMessages.History) { messages.Add(message); }
            }

            foreach (var entry in messages)
            {
                Console.ForegroundColor = entry.Color;                              // Set message color
                Console.BackgroundColor = entry.Highlight;                          // Highlight text
                Console.SetCursorPosition(entry.XVal, entry.YVal);                  // Set x & y cursor co-ordinates
                Console.Write(entry.Contents);                                      // Write message contents
                for (int i = 0; i < entry.NewLines; i++) { Console.WriteLine(""); } // Set-up any new lines required
            }
        }
        public static void Write(string contents, int newLines, ConsoleColor color, ConsoleColor highlight, int x, int y, int sleep)
        {
            // -1 x & y is default code for current cursor position.
            if (x == -1) { x = Console.CursorLeft; }
            if (y == -1) { y = Console.CursorTop; }

            // Log the chat message so it can be re-written if the chat is updated or reset
            Log(new ConsoleMessage() { Contents = contents, NewLines = newLines, Color = color, Highlight = highlight, XVal = x, YVal = y });

            Console.ForegroundColor = color;
            Console.BackgroundColor = highlight;
            Console.SetCursorPosition(x, y);

            if (sleep != -1)
            {
                for (int i = 0; i < contents.Length; i++)
                {
                    Console.Write(contents[i]);
                    Thread.Sleep(sleep);
                }
            }
            else
            {
                Console.Write(contents);
            }

            for (int i = 0; i < newLines; i++) { Console.WriteLine(""); }
        }
    }

    // Print with multiple colors  §(15) = White [default] §(0) = Black  || See Colors.md for codes ||      [ONLY 1 HIGHLIGHT]
    public static void Print(string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor highlight = ConsoleColor.Black, int sleep = -1, ConsoleColor initColor = ConsoleColor.White)
    {
        ConsoleColor color = initColor;
        Regex rx = new Regex(@"\§\((\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        string[] texts = rx.Split(contents);
        // texts is a string array where every even index is a string and odd index is a color code

        for (int i = 0; i < texts.Length; i++)
        {
            // If it's an even index then its text to be Written
            if (i % 2 == 0)
            {
                // If last character in string, print the new lines aswell
                if (i == texts.Length - 1) { MainConsole.Write(texts[i], newLines, color, highlight, x, y, sleep); }
                else { MainConsole.Write(texts[i], 0, color, highlight, x, y, sleep); }
            }
            else // otherwise it's a color code
            {
                color = (ConsoleColor)int.Parse(texts[i]);
            }
        }
    }
    public static void Say(string speaker, string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor msgColor = ConsoleColor.White, ConsoleColor highlight = ConsoleColor.Black, int sleep = 55)
    {
        MainConsole.Write(speaker, 0, msgColor, highlight, x, y, sleep);
        contents = contents.Insert(0, ": ");

        Print(contents, newLines, x, y, highlight, sleep, msgColor);
    }
    static void CenterScreen(string title, string subtitle = "", int? time = null, string audioLocation = "")
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Clear();

        // Margin-top
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine("");
        }

        // Title & subtitle
        Console.SetCursorPosition((Console.WindowWidth - title.Length) / 2, Console.CursorTop);
        Console.WriteLine(title);
        Console.SetCursorPosition((Console.WindowWidth - subtitle.Length) / 2, (Console.CursorTop + 1));
        Console.WriteLine(subtitle);

        // Spacing to cursor
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine("");
        }
        Console.SetCursorPosition((Console.WindowWidth) / 2, (Console.CursorTop + 2));

        // Music & wait for keypress
        if (audioLocation != "")
        {
            AudioEngine.PlayMusic(audioLocation);
        }

        if (time.HasValue) { System.Threading.Thread.Sleep(time.Value); }
        else
        {
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                {
                    AudioEngine.StopMusic();
                    break;
                }
            }
        }
    }
    public class EscapeException : Exception { }
    public class EnterException : Exception { }
    static (int, int) HandleKeyPress(ConsoleLogs inputString, ConsoleKeyInfo keyPressed, int margin, int xPos, int yPos)
    {
        ConsoleColor color = ConsoleColor.Yellow;

        switch (keyPressed.Key)
        {
            case ConsoleKey.Escape: throw new EscapeException();
            case ConsoleKey.Enter: throw new EnterException();
            case ConsoleKey.Home: xPos = margin; break;
            case ConsoleKey.End: xPos = margin + inputString.Count; break;
            case ConsoleKey.LeftArrow: xPos = (xPos == margin) ? xPos : xPos - 1; break;                      // Don't move back if at start of string
            case ConsoleKey.RightArrow: xPos = (xPos == margin + inputString.Count) ? xPos : xPos + 1; break; // Don't move forward if at end of string

            case ConsoleKey.Backspace: // Backspace is just a delete with the cursor moved back one
                if (xPos != margin)   // If there is room to do so
                {
                    keyPressed = new ConsoleKeyInfo('\0', ConsoleKey.Delete, false, false, false); xPos--; // Creating a delete keypress
                    HandleKeyPress(inputString, keyPressed, margin, xPos, yPos);                           // Calling self to delete
                }
                break;

            case ConsoleKey.Delete:
                if (xPos != margin + inputString.Count)
                {
                    inputString.Remove(xPos, yPos);     // Remove character at cursor position
                    inputString.Shift(xPos, yPos, -1);  // Shift everything to the right of the cursor back by one
                    MainConsole.Refresh(inputString);   // Refresh screen
                }
                break;

            default:
                if (!char.IsControl(keyPressed.KeyChar)) // if key pressed isnt a control key (only visible characters)
                {
                    string letter = keyPressed.KeyChar.ToString();
                    inputString.Shift(xPos, yPos, 1);                                                                                    // Move everything infront of cursor to the right
                    inputString.Log(new ConsoleMessage() { Color = color, Contents = letter, XVal = xPos, YVal = yPos, NewLines = 0 });  // Log new character inputted
                    xPos++;                                                                                                              // Move cursor one step forward
                    MainConsole.Refresh(inputString);                                                                                    // Refresh screen
                }
                break;
        }
        return (xPos, yPos); // return new x and y co-ords
    }
    static string ReadChars()
    {
        string output = string.Empty;
        bool complete = false;
        int startPoint = Console.CursorLeft; // so that cursor does not go beyond starting point of text
        int x = startPoint; int y = Console.CursorTop;

        ConsoleLogs input = new ConsoleLogs();

        while (!complete)
        {
            Console.SetCursorPosition(x, y);
            ConsoleKeyInfo keyPressed = Console.ReadKey(true);
            try { (x, y) = HandleKeyPress(input, keyPressed, startPoint, x, y); }
            catch (EnterException) { complete = true; }
        }

        foreach (ConsoleMessage message in input.History)
        {
            output += message.Contents;
        }

        return output;
    }
    static int ReadInt(int xCoord = -1, int yCoord = -1)
    {
        while (true)
        {
            Print("§(14)> ", newLines: 0, x: xCoord, y: yCoord);
            string uInput = ReadChars();
            if (int.TryParse(uInput, out int result))
            {
                return result;
            }
            else
            {
                MainConsole.Refresh();
            }
        }
    }
    static string ReadStr(int xCoord = -1, int yCoord = -1, int maxLength = -1, bool pointer = false)
    {
        while (true)
        {
            if (pointer) { Print("§(14)> ", newLines: 0, x: xCoord, y: yCoord); } 
            string uInput = ReadChars();
            int len = uInput.Length;
            if (0 < len && (maxLength == -1 || len <= maxLength)) // insert more logical checks like is alphanumeric
            {
                return uInput;
            }
            else
            {
                MainConsole.Refresh();
            }
        }
    }



    static string name = "You";
    static void Prologue()
    {
        Say("Dave", "Ha, good one Greg!");
        Say("Greg", "I know right, I'm a real comedian!");
        Say( name,  "Um, actually that wasn't that funny...", msgColor: ConsoleColor.Yellow);
        Say("Greg", "What? Who's this kid?");
        Say( name,  "Close your mouth! The name's ", 0, msgColor: ConsoleColor.Yellow); 
        
        name = ReadStr();
        MainConsole.UpdateSpeaker("You", name);
        MainConsole.Refresh();
        Print($"§(14){name}.");

        Say("Greg", "What a ridiculous name.");
        Say("Dave", $"Hey, {name}'s my best friend!");
        Say("Greg", "...", sleep: 200);
        Say("Dave", "...", sleep: 200);
        Say("Dave", $"LOL! Just kidding! Bye, {name}!");
        Print("§(8)Dave pushes you off the ship.", sleep: 84);
        Print("§(8)You fall.", sleep: 84);
        Print("§(8)You survive.", sleep:84);
        Print("§(8)With a minor major interior exterior concussion.", sleep: 84);
        Thread.Sleep(1500);

        MainConsole.Clear();
        CenterScreen("Hours pass...", time:2000);
        MainConsole.Clear();
    }
    static void Chapter1()
    {
        Print("§(8)You awake.", sleep: 84);
        Say(name, "Uhh... what happened?", msgColor: ConsoleColor.Yellow);
        Say(name, "Wait, what? Am I on a deserted island?", msgColor: ConsoleColor.Yellow);
        Say(name, "Oh that Dave! What a prankster!", msgColor: ConsoleColor.Yellow);
        Say(name, "What in the world should I do now?", 2, msgColor: ConsoleColor.Yellow);

    }

    static void Main(string[] args)
    {
        Prologue();
        while (Console.KeyAvailable) { Console.ReadKey(false); } // clear consolekey buffer

        Chapter1();
    }
}