using System;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security;
using System.Collections;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using NAudio;
using NAudio.Codecs;
using NAudio.Dmo;
using NAudio.Wave;
using static System.Net.Mime.MediaTypeNames;
using static Castaway.Program;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.ExceptionServices;
using Castaway.Audio;

namespace Castaway;

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
        public void ClearBetween(int yMin, int yMax) { ConsoleHistory.RemoveAll(s => s.YVal > yMin && s.YVal < yMax); }
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
        public static void Clear(int yMin, int yMax)
        {
            TheConsole.ClearBetween(yMin, yMax);
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
    static Regex colorsRx = new Regex(@"\§\((\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static CachedSound menuBleep = new CachedSound("MenuBleepSoft.wav");
    static CachedSound menuSelect = new CachedSound("MenuSelect.wav");

    static string[] FormatParser(string textToParse)
    {
        string[] texts = colorsRx.Split(textToParse);
        return texts;
    }
    static string RemoveColorCodes(string textToStrip)
    {
        string stripped = colorsRx.Replace(textToStrip, "");
        return stripped;
    }
    // Print with multiple colors  §(15) = White [default] §(0) = Black  || See Colors.md for codes ||      [ONLY 1 HIGHLIGHT]
    public static void Print(string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor highlight = ConsoleColor.Black, int sleep = -1, ConsoleColor initColor = ConsoleColor.White)
    {
        ConsoleColor color = initColor;
        string[] texts = FormatParser(contents);
        // texts is a string array where every even index is a string and odd index is a color code
        for (int i = 0; i < texts.Length; i++)
        {
            // If it's an even index then its text to be Written
            if (i % 2 == 0)
            {
                // If last character in string, print the new lines aswell
                if (i == texts.Length - 1) { MainConsole.Write(texts[i], newLines, color, highlight, x, y, sleep); }
                else { MainConsole.Write(texts[i], 0, color, highlight, x, y, sleep); }
                x = Console.CursorLeft;
                y = Console.CursorTop;
            }
            else // otherwise it's a color code
            {
                color = (ConsoleColor)int.Parse(texts[i]);
            }
        }
    }
    public static void Say(string speaker, string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor msgColor = ConsoleColor.White, ConsoleColor highlight = ConsoleColor.Black, int sleep = 55)
    {
        if (speaker == name) { msgColor = ConsoleColor.Cyan; }
        MainConsole.Write(speaker, 0, msgColor, highlight, x, y, sleep);
        contents = contents.Insert(0, ": ");

        Print(contents, newLines, x, y, highlight, sleep, msgColor);
    }
    public static void Narrate(string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor msgColor = ConsoleColor.Gray, ConsoleColor highlight = ConsoleColor.Black, int sleep = 84)
    {
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
            AudioPlaybackEngine.Instance.PlayLoopingMusic(audioLocation);
        }

        if (time.HasValue) { Thread.Sleep(time.Value); }
        else
        {
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                {
                    AudioPlaybackEngine.Instance.StopLoopingMusic();
                    break;
                }
            }
        }
    }
    //public class EscapeException : Exception { }
    public class EnterException : Exception { }
    static (int, int) HandleKeyPress(ConsoleLogs inputString, ConsoleKeyInfo keyPressed, int margin, int xPos, int yPos)
    {
        ConsoleColor color = ConsoleColor.Yellow;

        switch (keyPressed.Key)
        {
            //case ConsoleKey.Escape: throw new EscapeException();
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
    static int Choose(string[] options, bool escapable = true, CachedSound? mainSelectSound = null)
    {
        mainSelectSound ??= menuSelect;
        int choice = 0;
        int indent = (Console.WindowWidth / 2) - (options.Sum(o => o.Length + 10) / 2);
        int xIndent = indent;
        int yIndent = Console.WindowHeight - (3 + 3);
        bool chosen = false;
        while (!chosen)
        {
            Console.CursorVisible = false;
            xIndent = indent;

            // write all options with current selected highlighted
            for (int i = 0; i < options.Length; i++)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Black;
                Console.SetCursorPosition(xIndent, yIndent);
                Console.WriteLine(new String('-', options[i].Length + 4));
                Console.SetCursorPosition(xIndent, yIndent + 1);
                Console.Write("| ");
                if (choice == i)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.BackgroundColor = ConsoleColor.DarkGray;
                    Console.Write(options[i]);
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.Black;
                }
                else
                {
                    Console.Write(options[i]);
                }
                Console.WriteLine(" |");
                Console.SetCursorPosition(xIndent, yIndent + 2);
                Console.Write(new String('-', options[i].Length + 4));

                xIndent += options[i].Length + 10;
            }

            switch (Console.ReadKey(true).Key)
            {
                case ConsoleKey.RightArrow:
                    if (choice < options.Length - 1)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        choice++;
                        Console.Clear();
                        MainConsole.Refresh();
                    }; break;
                case ConsoleKey.LeftArrow:
                    if (choice > 0)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        choice--;
                        Console.Clear();
                        MainConsole.Refresh();
                    }
                    break;
                case ConsoleKey.Spacebar:
                case ConsoleKey.Enter:
                    if (choice == 0) { AudioPlaybackEngine.Instance.PlaySound(mainSelectSound); }
                    else { AudioPlaybackEngine.Instance.PlaySound(menuSelect); }
                    chosen = true;
                    break;
                case ConsoleKey.Escape: if (escapable) { choice = -1; chosen = true; } break;
            }

            Console.CursorVisible = true;
        }

        MainConsole.Clear();
        return choice;
    }
    static void CenterText(string[] input, int? time = null, int marginTop = 10, string audioLocation = "", Dictionary<int, int>? colors = null, bool anyKey = false)
    {
        if (colors == null)
        {
            for (int i = 0; i < input.Length; i++)
            {
                int length = RemoveColorCodes(input[i]).Length;
                Print(input[i], 1, (Console.WindowWidth - length) / 2, marginTop + i);
            }
        }
        else
        {
            for (int i = 0; i < input.Length; i++)
            {
                int length = RemoveColorCodes(input[i]).Length;
                if (colors.ContainsKey(i))
                {
                    Print($"§({colors[i]}){input[i]}", 1, (Console.WindowWidth - length) / 2, marginTop + i);
                }
                else
                {
                    Print(input[i], 1, (Console.WindowWidth - length) / 2, marginTop + i);
                }
            }
        }
        Console.SetCursorPosition(Console.WindowWidth / 2, Console.WindowHeight - 10);

        // Music & wait for keypress
        if (audioLocation != "")
        {
            AudioPlaybackEngine.Instance.PlayLoopingMusic(audioLocation);
        }

        if (time.HasValue)
        {
            Thread.Sleep(time.Value);
        }

        else
        {
            while (true)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Enter || anyKey && Console.KeyAvailable )
                {
                    AudioPlaybackEngine.Instance.StopLoopingMusic();
                    break;
                }
            }
        }
    }

    static void DigArt(int artChoice, int speed = 0)
    {
        string art = string.Empty;
        bool center = false;

        switch (artChoice)
        {
            case 0: center = true; art = @"

██████╗ █████╗ ███████╗████████╗ █████╗ ██╗    ██╗ █████╗ ██╗   ██╗
██╔════╝██╔══██╗██╔════╝╚══██╔══╝██╔══██╗██║    ██║██╔══██╗╚██╗ ██╔╝
██║     ███████║███████╗   ██║   ███████║██║ █╗ ██║███████║ ╚████╔╝ 
██║     ██╔══██║╚════██║   ██║   ██╔══██║██║███╗██║██╔══██║  ╚██╔╝  
╚██████╗██║  ██║███████║   ██║   ██║  ██║╚███╔███╔╝██║  ██║   ██║   
╚═════╝╚═╝  ╚═╝╚══════╝   ╚═╝   ╚═╝  ╚═╝ ╚══╝╚══╝ ╚═╝  ╚═╝   ╚═╝   
                                                            "; break;
        }

        if (center)
        {
            using (StringReader reader = new StringReader(art))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    int cursorPos = (Console.WindowWidth - line.Length) / 2;
                    Print(line, 1, cursorPos);
                    Thread.Sleep(speed);
                }
            }
        }
        else
        {
            using (StringReader reader = new StringReader(art))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    Print(line);
                }
            }
        }
    }
    public class CharismaZeroException : Exception { }
    public class WonGameException : Exception { }

    static string name = "You";
    // [0] Fun | [1] Charisma | [2] BaseAttackDamage | [3] PuppiesSaved | [4] BladesOfGrassTouched
    static int[] stats = new int[5] { 0, 90, 0, 0, 0 };
    static Dictionary<string, int> inventory = new Dictionary<string, int> { { "Gold", 25 } };
    static Random rand = new Random();
    static void AddToInventory(string key, int value)
    {
        if (inventory.ContainsKey(key)) { inventory[key] += value; }
        else { inventory.Add(key, value); }
    }
    static void AddCharisma(int charisma)
    {
        int newCharisma = stats[1] + charisma;
        if (newCharisma > 100)
        {
            stats[1] = 100;
        }
        else if (newCharisma < 1)
        {
            if (stats[1] == 1) { throw new CharismaZeroException(); }
            else
            {
                stats[1] = 1;
                Narrate("§(12)Your charisma is vitally low!");
            }
        }
        else
        {
            stats[1] = newCharisma;
        }        
    }

    static void Forage()
    {
        MainConsole.Clear();
        int luck = rand.Next(0, 101);
        if (luck < 20)
        {
            int sticks = rand.Next(2, 6);
            Narrate($"You find {sticks} sticks.");
            AddToInventory("Sticks", sticks);
            Narrate($"Your §(12)charisma §(7)goes up by 1.");
            AddCharisma(1);
        }
        else if (luck < 50)
        {
            int coconuts = rand.Next(1, 4);
            Narrate($"You found {coconuts} coconut{(coconuts != 1 ? "s" : "")}!");
            AddToInventory("Coconuts", coconuts);
            Narrate($"Your §(12)charisma §(7)goes up by 10.");
            AddCharisma(10);
        }
        else if (luck < 80)
        {
            if (stats[0] < 20)
            {
                Narrate("You find... used toilet paper?");
                AddToInventory("Used TP", 1);
                Narrate($"Your §(12)charisma §(7)goes down by 1.");
                AddCharisma(-1);
            }
            else
            {
                Narrate("You only found more sand.");
                Narrate($"Your §(12)charisma §(7)goes down by 10.");
                AddCharisma(-10);
            }
        }
        else
        {
            int gold = rand.Next(10, 50);
            Narrate($"You found {gold} gold!");
            AddToInventory("Gold", gold);
            Narrate($"Your §(12)charisma §(7)goes up by 10.");
            AddCharisma(20);
        }
    }
    static void DrawDigGame(int[,] map, int x, int y)
    {
        MainConsole.Refresh();
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, 2);

        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                Console.ForegroundColor = ConsoleColor.White;
                if (col == x && row == y) { Console.BackgroundColor = ConsoleColor.DarkGray; }
                switch (map[row, col])
                {
                    case 0:
                        Console.Write("#"); break;
                    case 1:
                        Console.Write(" "); break;
                    case 2:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("o"); break;
                    case 3:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write("*"); break;
                    case 4:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write("|"); break;
                }
                Console.BackgroundColor = ConsoleColor.Black;
            }
            Console.WriteLine("");
        }
    }
    static void DigGame()
    {
        CachedSound sandDestroy = new CachedSound("DigSand.wav");

        MainConsole.Clear();

        // [0] sand | [1] empty | [2] gold coin | [3] diamond | [4] stick 
        int[,] map = new int[5, 5];

        int xChoice = 0, yChoice = 0;
        for (int choices = 0; choices < 5; choices++)
        {
            Print($"You have {5 - choices} choice{(choices == 4 ? "" : "s")} remaining. ", 2, 0, 0);

            bool chosen = false;
            while (!chosen)
            {
                //MainConsole.Clear(1, 7);
                DrawDigGame(map, xChoice, yChoice);

                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.RightArrow:
                        if (xChoice < 4)
                        {
                            xChoice++;
                            Console.Clear();
                            MainConsole.Refresh();
                        }
                        break;
                    case ConsoleKey.LeftArrow:
                        if (xChoice > 0)
                        {
                            xChoice--;
                            Console.Clear();
                            MainConsole.Refresh();
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if (yChoice < 4)
                        {
                            yChoice++;
                            Console.Clear();
                            MainConsole.Refresh();
                        }
                        break;
                    case ConsoleKey.UpArrow:
                        if (yChoice > 0)
                        {
                            yChoice--;
                            Console.Clear();
                            MainConsole.Refresh();
                        }
                        break;
                    case ConsoleKey.Spacebar:
                    case ConsoleKey.Enter:
                        if (map[yChoice, xChoice] == 0)
                        {

                            AudioPlaybackEngine.Instance.PlaySound(sandDestroy);
                            chosen = true; 
                        }
                        break;
                }
                Console.CursorVisible = true;
            }

            int luck = rand.Next(0, 101);
            string item;
            string count;
            int charisma;
            if (luck < 50)
            {
                count = "absolutely";
                item = "§(7)nothing";
                map[yChoice, xChoice] = 1;
                charisma = -10;
            }
            else if (luck < 70)
            {
                count = rand.Next(2, 6).ToString();
                item = "§(6)sticks";
                map[yChoice, xChoice] = 4;
                AddToInventory("Sticks", int.Parse(count));
                charisma = -1;
            }
            else if (luck < 90)
            {
                count = rand.Next(8, 35).ToString();
                item = "§(14)gold";
                map[yChoice, xChoice] = 2;
                AddToInventory("Gold", int.Parse(count));
                charisma = 10;
            }
            else
            {
                count = "a";
                item = "§(9)diamond";
                map[yChoice, xChoice] = 3;
                AddToInventory("Diamond", 1);
                charisma = 50;

            }

            Print($"You found {count} {item}§(15)!", 1, 0, 8 + choices*2);
            Print($"§(7)Your §(12)charisma §(7)goes {(charisma < 0 ? "down" : "up")} by {Math.Abs(charisma)}.");
            AddCharisma(charisma);
        }

        Print($"You have 0 choices remaining.", 2, 0, 0);
        DrawDigGame(map, -1, -1);
        Narrate("Press any key to continue.", 0, 0, 18);
        while (Console.KeyAvailable) { Console.ReadKey(false); } // clear consolekey buffer
        Console.ReadKey();
        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
        MainConsole.Clear();
    }

    static void Prologue()
    {
        MainConsole.Clear();
        Narrate("17:23. Ocean Atlantic Cruise", 2);
        Say("Dave", "Ha, good one Greg!");
        Say("Greg", "I know right, I'm a real comedian!");
        Say(name, "Um, actually that wasn't that funny...");
        Say("Greg", "What? Who's this kid?");
        Say(name, "Close your mouth! The name's ", 0);

        name = ReadStr();
        MainConsole.UpdateSpeaker("You", name);
        MainConsole.Refresh();
        Narrate($"§(14){name}.");

        Say("Greg", "What a ridiculous name.");
        Say("Dave", $"Hey, {name}'s my best friend!");
        Say("Greg", "...", sleep: 200);
        Say("Dave", "...", sleep: 200);
        Say("Dave", $"LOL! Just kidding! Bye, {name}!");
        Narrate("Dave pushes you off the ship.");
        Narrate("You fall.");
        Narrate("You survive.");
        Narrate("With a minor major interior exterior concussion.");
        Thread.Sleep(1500);

        MainConsole.Clear();
        CenterScreen("Hours pass...", time: 2000);
        MainConsole.Clear();
    }
    static void Chapter1()
    {
        Narrate("You awake.");
        Say(name, "Uhh... what happened?");
        Say(name, "Wait, what? Am I on a deserted island?");
        Say(name, "Oh that Dave! What a prankster!");

        bool finished = false;
        while (!finished)
        {
            Say(name, "What in the world should I do now?", 2);

            int choice = Choose(new string[] { "Forage for food", "Dig for gold", "Shout for help", "Sleep", "View stats" });
            switch (choice)
            {
                case -1: MainMenu(); break;
                case 0: Forage(); break;
                case 1: DigGame(); break;
                case 2:
                    Say(name, "Help me!");
                    Thread.Sleep(1000);
                    int chance = rand.Next(1, 101);
                    if (chance < 70)
                    {
                        Narrate("No-one responds.");
                        Narrate($"Your§(12) charisma §(7)drops by 10.");
                        AddCharisma(-10);
                    }
                    else if (chance < 99)
                    {
                        Say("Dave", "What in the world are you still doing here?");
                        Say(name, "Dave you came back!");
                        Say("Dave", "And I'll leave as soon as I came.");
                        Say(name, "Wait! Please help me out! I'm in dire need of your assistance!");
                        Say("Dave", "Ugh okay, here's a §(9)diamond.");
                        Narrate("§(13) Maybe the someone will be interested in this...");
                        Narrate($"Your§(12) charisma §(7)is restored to 100.");
                        AddCharisma(100);
                        Say(name, "See you later Dave!");
                        Narrate("Dave disappears round a corner.");
                        Say(name, "I probably should've followed him...");
                        Narrate($"Your§(12) charisma §(7)drops by 5.");
                        AddCharisma(-5);
                    }
                    else
                    {
                        Say("Greg", $"Ugh what is it now, {name}.");
                        Say(name, "Please help me get off this island Greg!");
                        Say("Greg", "Ugh, fine. Get on the boat");
                        Narrate("Greg takes you back home.");
                        throw new WonGameException();
                    }
                    break;
                case 3:
                    Narrate("You lay down and try to sleep.");
                    Narrate("You can't. It's midday.");
                    Narrate($"Your §(12) charisma §(7)drops by 5.");
                    AddCharisma(-5);
                    break;
                case 4: ViewStats(); break;
            }
        }
    }

    static void Instructions()
    {
        MainConsole.Clear();
        DigArt(0, 0);
        string[] messages = new string[] { "Survive being cast away on a desert island!",
                                           "",
                                           "§(12)Charisma §(15)is your will to continue.",
                                           "Don't let it fall to 0, and make sure to monitor it often!",
                                           "",
                                           "Press escape to view the main menu during the game.",
                                           "",
                                           "",
                                           "Press any key to return to the main menu." };
        Dictionary<int, int> colorScheme = new Dictionary<int, int>() { { 8, 8 } };
        // this means the line at index 8 (press any key...) will be colour 8 == ConsoleColor.DarkGray
        CenterText(messages, colors:colorScheme, anyKey:true);
        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
        MainConsole.Clear();

    }
    static void ViewInventory()
    {
        MainConsole.Clear();
        foreach (KeyValuePair<string,int> item in inventory)
        { 
            Print($"{item.Key}: {item.Value}");
        }
        Print("", 1);
        Print("§(8)Press any key to continue.");
        Console.ReadKey();

        MainConsole.Clear();
    }
    static void ViewStats()
    {
        MainConsole.Clear();
        for (int i = 1; i < stats.Length; i++) // skipping fun value (they cant know that mwahahaha)
        {
            string stat = string.Empty;
            switch (i)
            {
                case 1: stat = "§(12)Charisma"; break;
                case 2: stat = "Puppies saved"; break;
            }
            Print($"{stat}§(15): {stats[i]}");
        }
        Print("", 1);
        Print("§(8)Press any key to continue.");
        Console.ReadKey();

        MainConsole.Clear();
    }
    static void MainMenu()
    {
        bool usingMainMenu = true;
        while (usingMainMenu)
        {
            DigArt(0);
            int choice = Choose(new string[] { "Return To Game", "Inventory", "View Stats", "New Game", "Exit" });
            switch (choice)
            {
                case 0: usingMainMenu = false; break;
                case 1: ViewInventory(); break;
                case 2: ViewStats(); break;
                case 3: throw new NewGameException();
                case 4: Environment.Exit(0); break;
            }
        }
    }

    static void NewGame()
    {
        Prologue();

        while (Console.KeyAvailable) { Console.ReadKey(false); } // clear consolekey buffer

        MainConsole.Clear();
        Chapter1();
    }

    public class NewGameException : Exception { }
    static void Main(string[] args)
    {
        stats[0] = rand.Next(0,101); // random 'fun' value (stole that idea from undertale)

        bool usingStartMenu = true;
        while (usingStartMenu)
        {
            DigArt(0, 100);
            int choice = Choose(new string[] { "New Game", "Instructions", "Exit" }, false, new CachedSound("GameStart.wav"));
            switch (choice)
            {
                case 0: usingStartMenu = false; break;
                case 1: Instructions(); break;
                case 2: Environment.Exit(0); break;
            }
        }

        while (true)
        {
            try
            {
                NewGame();
            }
            catch (NewGameException)
            {
                name = "You";
                stats[0] = rand.Next(0, 101);
            }
        }
    }
}