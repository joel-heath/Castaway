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
using System.Xml;
using System.Data;
using static Castaway.Program.ConsoleMessage;
using NAudio.SoundFont;


namespace Castaway;
class Program
{
    public class ConsoleMessage
    {
        public string Contents { get; set; } = string.Empty;
        public int Length() { return RemoveColorCodes(Contents).Length; }
        public int NewLines { get; set; } = 1;
        public ConsoleColor Color { get; set; } = ConsoleColor.White;
        public ConsoleColor Highlight { get; set; } = ConsoleColor.Black;
        public int XVal { get; set; } = 0;
        public int YVal { get; set; } = 0;
        public ConsoleMessage(string contents, int newLines, ConsoleColor color, ConsoleColor highlight, int xVal, int yVal) : this(contents, color)
        {
            this.NewLines = newLines;
            this.Highlight = highlight;
            this.XVal = xVal;
            this.YVal = yVal;
        }
        public ConsoleMessage(string contents, ConsoleColor color) : this(contents)
        {
            this.Color = color;
        }
        public ConsoleMessage(string contents)
        {
            this.Contents = contents;
        }

        public static implicit operator ConsoleMessage(string s) => new ConsoleMessage(s);

        // Turns: new ConsoleMessage[] { new ConsoleMessage("Hello"), new ConsoleMessage("Hi") };
        // Into:  Build("Hello", "Hi");

        /* old way bad
        public static ConsoleMessage[] Build(params string[] messages)
        {
            return messages.Select(m => new ConsoleMessage(m)).ToArray();
        }
        */
        public static ConsoleMessage[] Build(params ConsoleMessage[] messages)
        {
            return messages;
        }

        // The following is used for building a shop menu. Probably way to overcomplicated but oh well

        /* Turns:
         *  new Tuple<ConsoleMessage>[][] { new Tuple<ConsoleMessage, int>[] { new Tuple<ConsoleMessage, int>("Hello", 5), new Tuple<ConsoleMessage, int>("Hi", 10) },
         *                                  new Tuple<ConsoleMessage, int>[] { new Tuple<ConsoleMessage, int>("Yo", 4), new Tuple<ConsoleMessage, int>("Howdy", 20) }, }
         * Into:
         *  BuildShelves(NewShelf( NewItem("Hello", 5), NewItem("Hi", 10)),
         *               NewShelf( NewItem("Yo", 4), NewItem("Howdy", 20)));
         */
        public static Tuple<ConsoleMessage, int> NewItem(ConsoleMessage item, int cost)
        {
            return new Tuple<ConsoleMessage, int>(item, cost);
        }
        public static Tuple<ConsoleMessage, int>[] NewShelf(params Tuple<ConsoleMessage, int>[] items)
        {
            return items;
        }
        public static Tuple<ConsoleMessage, int>[][] BuildShelves(params Tuple<ConsoleMessage, int>[][] shelves)
        {
            return shelves;
        }
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
            /*ConsoleMessage? removeMe = new ConsoleMessage { XVal = -1 }; // fake item that can't exist, so nothing will be removed if item is not found
            foreach (ConsoleMessage log in ConsoleHistory)
            {
                if (log.YVal == y && log.XVal == x) { removeMe = log; } // if item is found remove it
            }
            ConsoleHistory.Remove(removeMe);
            */
            ConsoleHistory.RemoveAll(s => s.XVal == x && s.YVal == y);
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
        public static void Write(string contents, int newLines, ConsoleColor color, ConsoleColor highlight, int x, int y, int sleep, CachedSound? voice, bool logMessage)
        {
            // -1 x & y is default code for current cursor position.
            if (x == -1) { x = Console.CursorLeft; }
            if (y == -1) { y = Console.CursorTop; }

            // Log the chat message so it can be re-written if the chat is updated or reset
            if (logMessage) { Log(new ConsoleMessage(contents, newLines, color, highlight, x, y)); }

            Console.ForegroundColor = color;
            Console.BackgroundColor = highlight;
            Console.SetCursorPosition(x, y);

            if (sleep == -1)
            {
                Console.Write(contents);
            }
            else
            {
                foreach (var c in contents)
                {
                    if (voice != null) AudioPlaybackEngine.Instance.PlaySound(voice);
                    Thread.Sleep(sleep);
                    Console.Write(c);
                }
            }

            for (int i = 0; i < newLines; i++) { Console.WriteLine(""); }
        }
    }

    static Regex colorsRx = new Regex(@"\§\((\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
    public static void Print(string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor highlight = ConsoleColor.Black, int sleep = -1, ConsoleColor initColor = ConsoleColor.White, CachedSound? voice = null, bool logMessage = true)
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
                if (i == texts.Length - 1) { MainConsole.Write(texts[i], newLines, color, highlight, x, y, sleep, voice, logMessage); }
                else { MainConsole.Write(texts[i], 0, color, highlight, x, y, sleep, voice, logMessage); }
                x = Console.CursorLeft;
                y = Console.CursorTop;
            }
            else // otherwise it's a color code
            {
                color = (ConsoleColor)int.Parse(texts[i]);
            }
        }
    }
    public static void Narrate(string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor msgColor = ConsoleColor.Gray, ConsoleColor highlight = ConsoleColor.Black, int sleep = 84)
    {
        Print(contents, newLines, x, y, highlight, sleep, msgColor);
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
                    inputString.Log(new ConsoleMessage(letter, 0, color, Console.BackgroundColor, xPos, yPos));  // Log new character inputted
                    xPos++;                                                                                                              // Move cursor one step forward
                    MainConsole.Refresh(inputString);                                                                                    // Refresh screen
                }
                break;
        }
        return (xPos, yPos); // return new x and y co-ords
    }
    static void ClearKeyBuffer()
    {
        while (Console.KeyAvailable) { Console.ReadKey(true); } // clear consolekey buffer
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
        int xInit, yInit;
        if (xCoord == -1) { xInit = Console.CursorLeft; }
        else { xInit = xCoord; }
        if (yCoord == -1) { yInit = Console.CursorTop; }
        else { yInit = yCoord; }

        while (true)
        {
            if (pointer) { Print("§(14)> ", newLines: 0, x: xCoord, y: yCoord); }
            string uInput = ReadChars();
            int len = uInput.Length;
            if (0 < len && (maxLength == -1 || len <= maxLength)) // insert more logical checks like is alphanumeric
            {
                Console.SetCursorPosition(xInit, yInit);
                return uInput;
            }
            else
            {
                MainConsole.Refresh();
            }
        }
    }
    static int Choose(ConsoleMessage[] options, bool escapable = true, CachedSound? mainSelectSound = null)
    {
        ClearKeyBuffer();
        mainSelectSound ??= menuSelect;
        int choice = 0;
        int indent = (Console.WindowWidth / 2) - (options.Sum(o => o.Length() + 10) / 2);
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
                Console.WriteLine(new String('-', options[i].Length() + 4));
                Console.SetCursorPosition(xIndent, yIndent + 1);
                Console.Write("| ");
                if (choice == i)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.BackgroundColor = ConsoleColor.DarkGray;
                }
                else
                {
                    Console.ForegroundColor = options[i].Color;
                }
                Console.Write(options[i].Contents);
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Black;

                Console.WriteLine(" |");
                Console.SetCursorPosition(xIndent, yIndent + 2);
                Console.Write(new String('-', options[i].Length() + 4));

                xIndent += options[i].Length() + 10;
            }

            switch (Console.ReadKey(true).Key)
            {
                case ConsoleKey.RightArrow:
                    if (choice < options.Length - 1)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        choice++;
                        MainConsole.Refresh();
                    }; break;
                case ConsoleKey.LeftArrow:
                    if (choice > 0)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        choice--;
                        MainConsole.Refresh();
                    }
                    break;
                case ConsoleKey.Spacebar:
                case ConsoleKey.Enter:
                    if (choice == 0) { AudioPlaybackEngine.Instance.PlaySound(mainSelectSound); }
                    else { AudioPlaybackEngine.Instance.PlaySound(menuSelect); }
                    chosen = true;
                    break;
                case ConsoleKey.Escape:
                    if (escapable)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(pauseSound);
                        choice = -1;
                        chosen = true;
                    }
                    break;
            }

            Console.CursorVisible = true;
        }
        MainConsole.Refresh();
        //MainConsole.Clear();
        return choice;
    }
    static int SimpleChoose(ConsoleMessage[] options, bool escapable = true, CachedSound? mainSelectSound = null)
    {
        mainSelectSound ??= menuSelect;
        int choice = 0;
        bool chosen = false;
        int yIndent = Console.WindowHeight - 5;
        while (!chosen)
        {
            Console.CursorVisible = false;
            int y = yIndent;
            // write all options with current selected highlighted
            for (int i = 0; i < options.Length; i++)
            {
                int x = (Console.WindowWidth - options[i].Length()) / 2;
                Console.SetCursorPosition(x, y);
                if (choice == i)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.BackgroundColor = ConsoleColor.DarkGray;
                }
                else
                {
                    Console.ForegroundColor = options[i].Color;
                    Console.BackgroundColor = ConsoleColor.Black;
                }
                Console.Write(options[i].Contents);

                y++;
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;

            ClearKeyBuffer();
            switch (Console.ReadKey(true).Key)
            {
                case ConsoleKey.DownArrow:
                    if (choice < options.Length - 1)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        choice++;
                        MainConsole.Refresh();
                    }; break;
                case ConsoleKey.UpArrow:
                    if (choice > 0)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        choice--;
                        MainConsole.Refresh();
                    }
                    break;
                case ConsoleKey.Spacebar:
                case ConsoleKey.Enter:
                    if (choice == 0) { AudioPlaybackEngine.Instance.PlaySound(mainSelectSound); }
                    else { AudioPlaybackEngine.Instance.PlaySound(menuSelect); }
                    chosen = true;
                    break;
                case ConsoleKey.Escape:
                    if (escapable)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(pauseSound);
                        choice = -1;
                        chosen = true;
                    }
                    break;
            }

            Console.CursorVisible = true;
        }

        MainConsole.Refresh();
        return choice;
    }
    static void CenterText(ConsoleMessage[] input, int? time = null, int marginTop = 10, string audioLocation = "")
    {
        for (int i = 0; i < input.Length; i++)
        {
            int length = input[i].Length();
            Print($"§({(int)input[i].Color}){input[i].Contents}", 1, (Console.WindowWidth - length) / 2, marginTop + i);
        }

        Console.SetCursorPosition(Console.WindowWidth / 2, Console.WindowHeight - 10);

        // Sfx & wait for keypress
        if (audioLocation != "")
        {
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(audioLocation));
        }

        if (time.HasValue)
        {
            Thread.Sleep(time.Value);
        }
        else
        {
            Console.ReadKey(true);
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
                CachedSound sound = new CachedSound("DigSand.wav");
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (speed != 0)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(sound);
                        int cursorPos = (Console.WindowWidth - line.Length) / 2;
                        Print(line, 1, cursorPos);
                        Thread.Sleep(speed);
                    }
                    else
                    {
                        int cursorPos = (Console.WindowWidth - line.Length) / 2;
                        Print(line, 1, cursorPos);
                    }
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
    class Person
    {
        public string Name { get; set; }
        public ConsoleColor Color { get; set; }
        public CachedSound? Voice { get; set; }
        public Person(string name, ConsoleColor color, string? audioLocation)
        {
            this.Name = name;
            this.Color = color;
            if (audioLocation == null) { this.Voice = null; }
            else { this.Voice = new CachedSound(audioLocation); }
        }
        public void Say(string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor highlight = ConsoleColor.Black, int sleep = 55)
        {
            MainConsole.Write(Name, 0, Color, highlight, x, y, sleep, Voice, true);
            contents = contents.Insert(0, ": ");

            Print(contents, newLines, x, y, highlight, sleep, Color, Voice);
        }

    }

    // [0] Fun | [1] Charisma | [2] BaseAttackDamage | [3] PuppiesSaved | [4] BladesOfGrassTouched
    static int[] stats = new int[5];
    static List<string> knowledge = new List<string>(); // Used for storing whether player has entered a house and stuff
    static Dictionary<string, int> inventory = new Dictionary<string, int>();
    static Random rand = new Random();
    static CachedSound menuBleep = new CachedSound("MenuBleepSoft.wav");
    static CachedSound menuSelect = new CachedSound("MenuSelected.wav");
    static CachedSound pauseSound = new CachedSound("PauseButton.wav");
    public class CharismaZeroException : Exception { }
    public class WonGameException : Exception { }

    static Person Player = new Person("You", ConsoleColor.Cyan, null); // "YouSpeak.wav"
    static int Buy(int cost)
    {
        if (!inventory.ContainsKey("Gold")) { return 0; } // returns amount of gold player has
        if (inventory["Gold"] < cost) { return inventory["Gold"]; }
        inventory["Gold"] -= cost;
        return -1; // -1 means they had sufficient gold
    }
    static void AddToInventory(string key, int value)
    {
        if (inventory.ContainsKey(key)) { inventory[key] += value; }
        else { inventory.Add(key, value); }
    }
    static int ItemCount(string key)
    {
        return inventory.ContainsKey(key) ? inventory[key] : 0;
    }
    static void AddCharisma(int charisma, bool narrate = true)
    {
        if (narrate) { Narrate($"§(7)Your §(12)charisma §(7) {(charisma < 0 ? "drops" : "goes up")} by {Math.Abs(charisma)}."); }
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
                Narrate("§(12)Your charisma is critically low!");
            }
        }
        else
        {
            stats[1] = newCharisma;
        }
    }
    static void RestoreCharisma()
    {
        Narrate($"§(7)Your §(12)charisma §(7)is restored to 100.");
        AddCharisma(100, false);
    }
    static void Instructions()
    {
        MainConsole.Clear();
        DigArt(0, 0);
        var messages = new ConsoleMessage[] { "Survive being cast away on a seemily remote desert island!",
                                              "",
                                              "§(12)Charisma §(15)is your will to continue.",
                                              "Don't let it fall to 0, and make sure to monitor it often!",
                                              "",
                                              "Press escape to view the main menu during the game.",
                                              "",
                                              "",
                                              "§(8)Press any key to return to the main menu." };
        CenterText(messages);
        MainConsole.Clear();
    }
    static void ViewInventory()
    {
        MainConsole.Clear();
        Print("Inventory", 2);
        foreach (KeyValuePair<string, int> item in inventory)
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
        Print("Statistics", 2);
        for (int i = 1; i < stats.Length; i++) // i=1, skipping fun value (they cant know that mwahahaha)
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
            int choice = Choose(new ConsoleMessage[] { "Return To Game", "Inventory", "View Stats", "New Game", "Exit" });
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
        stats[0] = rand.Next(0, 101); // random 'fun' value (stole that idea from undertale)
        stats[1] = 90; // Charisma level
        Player.Name = "You";
        inventory = new Dictionary<string, int> { { "Gold", 25 } };

        AudioPlaybackEngine.Instance.PlayLoopingMusic("Screen Saver.mp3");

        bool usingStartMenu = true;
        while (usingStartMenu)
        {
            DigArt(0, 100);
            int choice = Choose(Build("New Game", "Instructions", "Exit"), false, new CachedSound("GameStart.wav"));
            switch (choice)
            {
                case 0: usingStartMenu = false; break;
                case 1: Instructions(); break;
                case 2: Environment.Exit(0); break;
            }
        }

        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();



        Route1();

        /*
        AddToInventory("Gold", 50);
        AddToInventory("Diamonds", 2);
        AddToInventory("Sticks", 12);

        Shop();
        Route0();
        */



        /// actual main game

        /*Prologue();

ClearKeyBuffer();
        MainConsole.Clear();

        int route = Chapter1();
        
        ClearKeyBuffer();
        MainConsole.Clear();

        switch (route)
        {
            case 0: Route0(); break;
            case 1: Route1(); break;
            case 2: Route2(); break;
        }*/
    }


    /// CHAPTER 1
    static void Prologue()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Night in venice.mp3");
        Person dave = new Person("Dave", ConsoleColor.DarkCyan, null); // "DaveSpeak.wav"
        Person greg = new Person("Greg", ConsoleColor.Green, null); // "GregSpeak.wav"

        MainConsole.Clear();
        Narrate("17:23. Ocean Atlantic Cruise", 2);
        dave.Say("Ha, good one §(10)Greg§(3)!");
        greg.Say("I know right, I'm a real comedian!");
        Player.Say("Um, actually that wasn't that funny...");
        greg.Say("What? Who's this kid?");
        Player.Say("Close your mouth! The name's ", 0);

        Player.Name = ReadStr();
        Print($"§(11){Player.Name}.");
        MainConsole.UpdateSpeaker("You", Player.Name);
        MainConsole.Refresh();

        greg.Say("What a ridiculous name.");
        dave.Say($"Hey, §(11){Player.Name}§(3)'s my best friend!");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Crickets.wav");
        AudioPlaybackEngine.Instance.PlaySound(new CachedSound("AwkwardCough.wav"));
        greg.Voice = new CachedSound("GregSpeak.wav");
        dave.Voice = new CachedSound("YouSpeak.wav");
        Thread.Sleep(1000);
        greg.Say("...", sleep: 200);
        dave.Say("...", sleep: 200);
        greg.Voice = null;
        dave.Voice = null;
        Thread.Sleep(1000);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("OhNo.mp3");
        dave.Say($"LOL! Just kidding! Bye, §(11){Player.Name}§(3)!");
        Narrate("§(3)Dave §(7)pushes you off the cruise, into the unforgiving waters.");
        Narrate("You fall.");
        Narrate("You survive.");
        Narrate("With a minor major interior exterior concussion.");
        Thread.Sleep(1500);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        CenterText(Build("Days pass..."), time: 2000, audioLocation: "DaysPass.wav");
        MainConsole.Clear();
    }
    static int Chapter1()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("BeachSounds.wav");
        Thread.Sleep(1000);
        Narrate("You awake.");
        Player.Say("Uhh... what happened?");
        Player.Say("Wait, what? Am I on a deserted island?");
        Player.Say("Oh that §(3)Dave§(11)! What a prankster!");
        AudioPlaybackEngine.Instance.StopLoopingMusic();

        ConsoleMessage[] choices = Build("Forage for food", "Dig for gold", "Shout for help", "Sleep", "View stats");
        int route = -1;
        int loops = 0;

        AudioPlaybackEngine.Instance.PlayLoopingMusic("Moonlight beach.mp3");
        while (route == -1)
        {
            if (loops == 5)
            {
                return rand.Next(1, 3); // go down pirate or monkey route
            }
            Player.Say("What in the world should I do now?", 2);

            ClearKeyBuffer();
            if (inventory.ContainsKey("Diamonds"))
            {
                choices[2] = new ConsoleMessage("Shout 'I have a diamond'", ConsoleColor.Magenta);
            }
            int choice = Choose(choices);
            switch (choice)
            {
                case -1: MainMenu(); break;
                case 0: Forage(); break;
                case 1: DigGame(); break;
                case 2: route = Shout(inventory.ContainsKey("Diamonds")); break;
                case 3:
                    Narrate("You lay down and try to sleep.");
                    Narrate("You can't. It's midday.");
                    AddCharisma(-5);
                    break;
                case 4: ViewStats(); ViewInventory(); break;
            }
            loops++;
        }

        return route;
    }
    static void Forage()
    {
        MainConsole.Clear();
        int luck = rand.Next(0, 101);
        AudioPlaybackEngine.Instance.PauseLoopingMusic();
        AudioPlaybackEngine.Instance.PlaySound(new CachedSound("Discovery.wav"), true);
        AudioPlaybackEngine.Instance.ResumeLoopingMusic();

        if (luck < 20)
        {
            int sticks = rand.Next(2, 6);
            Narrate($"You find {sticks} sticks.");
            AddToInventory("Sticks", sticks);
            AddCharisma(1);
        }
        else if (luck < 50)
        {
            int coconuts = rand.Next(1, 4);
            Narrate($"You found {coconuts} coconut{(coconuts != 1 ? "s" : "")}!");
            AddToInventory("Coconuts", coconuts);
            AddCharisma(10);
        }
        else if (luck < 80)
        {
            if (stats[0] < 20)
            {
                Narrate("You find... used toilet paper?");
                AddToInventory("Used TP", 1);
                AddCharisma(-1);
            }
            else
            {
                Narrate("You only found more sand.");
                AddCharisma(-10);
            }
        }
        else
        {
            int gold = rand.Next(10, 50);
            Narrate($"You found {gold} gold!");
            AddToInventory("Gold", gold);
            AddCharisma(20);
        }
    }
    static int Shout(bool diamonds)
    {
        if (!diamonds)
        {
            Person dave = new Person("Dave", ConsoleColor.DarkCyan, "DaveSpeak.wav");
            Person greg = new Person("Greg", ConsoleColor.Green, "GregSpeak.wav");
            Player.Say("Help me!");
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
                dave.Say("What in the world are you still doing here?");
                Player.Say("§(3)Dave, §(11)you came back!");
                dave.Say("And I'll leave as soon as I came.");
                Player.Say("Wait! Please help me out! I'm in dire need of your assistance!");
                dave.Say("Ugh okay, here's a §(9)diamond§(3).");
                Narrate("§(13)Maybe someone will be interested in this...");
                RestoreCharisma();
                Player.Say("See you later §(3)Dave§(11)!");
                Narrate("§(3)Dave §(7)disappears round a corner.");
                Player.Say("I probably should've followed him...");
                Narrate($"Your§(12) charisma §(7)drops by 5.");
                AddCharisma(-5);
            }
            else
            {
                greg.Say($"Ugh what is it now, §(11){Player.Name}§(10).");
                Player.Say("Please help me get off this island, §(10)Greg§(11)!");
                greg.Say("Ugh, fine. Get on the boat");
                Narrate("§(10)Greg §(7)takes you back home.");
                throw new WonGameException();
            }
        }
        else
        {
            Person chanelle = new Person("Chanelle", ConsoleColor.DarkMagenta, null);
            Person addison = new Person("Addison", ConsoleColor.DarkBlue, null);
            Person shaniece = new Person("Shaniece", ConsoleColor.DarkGreen, null);
            Person mackenzie = new Person("Mackenzie", ConsoleColor.DarkRed, null);

            Player.Say("I have §(9)diamonds§(11)!");
            Thread.Sleep(1000);
            Narrate("The paparazzi appear, asking for your diamonds.");
            chanelle.Say("OMGOODNESS you have §(9)DIAMONDS§(5)!?");
            addison.Say($"WOW, §(11){Player.Name.ToUpper()}§(1), I LOVE YOU SO MUCH!");
            shaniece.Say($"You don't mind sharing do you?");
            mackenzie.Say($"Follow us, we'll give you whatever you want for your diamonds!");
            return 0; // route 0 'diamond route'
        }
        return -1; // carry on no route
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
        bool diamond = false;
        int xChoice = 0, yChoice = 0;
        for (int choices = 0; choices < 5; choices++)
        {
            Print($"You have {5 - choices} choice{(choices == 4 ? "" : "s")} remaining. ", 2, 0, 0);
            DrawDigGame(map, xChoice, yChoice);

            diamond = false;
            bool chosen = false;
            while (!chosen)
            {
                Console.CursorVisible = false;
                //MainConsole.Clear(1, 7);
                //DrawDigGame(map, xChoice, yChoice);

                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.RightArrow:
                        if (xChoice < 4)
                        {
                            xChoice++;
                            DrawDigGame(map, xChoice, yChoice);
                        }
                        break;
                    case ConsoleKey.LeftArrow:
                        if (xChoice > 0)
                        {
                            xChoice--;
                            DrawDigGame(map, xChoice, yChoice);
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if (yChoice < 4)
                        {
                            yChoice++;
                            DrawDigGame(map, xChoice, yChoice);
                        }
                        break;
                    case ConsoleKey.UpArrow:
                        if (yChoice > 0)
                        {
                            yChoice--;
                            DrawDigGame(map, xChoice, yChoice);
                        }
                        break;
                    case ConsoleKey.Spacebar:
                    case ConsoleKey.Enter:
                        if (map[yChoice, xChoice] == 0)
                        {
                            DrawDigGame(map, xChoice, yChoice);
                            AudioPlaybackEngine.Instance.PlaySound(sandDestroy);
                            chosen = true;
                        }
                        break;
                }
            }
            Console.CursorVisible = true;


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
                AddToInventory("Diamonds", 1);
                diamond = true;
                charisma = 50;

            }

            Print($"You found {count} {item}§(15)!", 1, 0, 8 + choices * 2);
            if (diamond) { Narrate("§(13)Maybe someone will be interested in this..."); }
            Print($"§(7)Your §(12)charisma §(7)goes {(charisma < 0 ? "down" : "up")} by {Math.Abs(charisma)}.");
            AddCharisma(charisma);
        }

        Print($"You have 0 choices remaining.", 2, 0, 0);
        DrawDigGame(map, -1, -1);
        if (diamond) { Narrate("§(13)Maybe someone will be interested in this..."); }
        Narrate("Press any key to continue.", 0, 0, 18);
        ClearKeyBuffer();
        Console.ReadKey();
        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
        MainConsole.Clear();
    }


    /// CHAPTER 2

    /// Diamond route
    static void Route0()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Fuzzball Parade.mp3");

        Person chanelle = new Person("Chanelle", ConsoleColor.DarkMagenta, null);
        Person addison = new Person("Addison", ConsoleColor.DarkBlue, null);
        Person shaniece = new Person("Shaniece", ConsoleColor.DarkGreen, null);
        Person mackenzie = new Person("Mackenzie", ConsoleColor.DarkRed, null);

        chanelle.Say($"Welcome to our village, {Player.Name}!");
        addison.Say("Have a look around!");
        shaniece.Say("Take a look at my hotel for a place to sleep at night.");
        mackenzie.Say("Check out my shop to spend that §(9)diamond §(4)of yours.");

        bool skipRamble = false; // cleans the transition to the east side
        bool finished = false;
        while (!finished)
        {
            if (!skipRamble) { Player.Say("What do I do now?"); }
            skipRamble = false;
            switch (Choose(Build("Mackenzie's Shop", "Shaniece's Hotel", "View stats", "Search Further East")))
            {
                case -1: MainMenu(); break;
                case 0: Shop(); break;
                case 1: Hotel(); break;
                case 2: ViewStats(); ViewInventory(); break;
                case 3: EastSide(); skipRamble = true; break;
            }

            if (!skipRamble) { MainConsole.Clear(); }
        }
    }
    static void EastSide()
    {
        bool finished = false;
        ConsoleMessage[] choices = { knowledge.Contains("Entered Greg's") ? "Greg's House" : "Small House",
                                     knowledge.Contains("Spoken with scammer") ? "Chanelle's House" : "Large House",
                                     "Sketchy Alleyway", "Return" };
        while (!finished)
        {
            int choice = Choose(choices);
            MainConsole.Refresh();
            switch (choice)
            {
                case 0: GregHouse(); choices[0] = "Greg's House"; break;
                case 1: ScammerHouse(); choices[1] = "Chanelle's House"; break;
                case 2: // sketchy allyway
                    AudioPlaybackEngine.Instance.StopLoopingMusic();
                    AudioPlaybackEngine.Instance.PlayLoopingMusic("Crickets.wav"); // quiet wind instead pls
                    MainConsole.Clear();
                    Narrate("There doesn't seem to be much around here.");
                    Narrate("...", sleep: 150);
                    if (!inventory.ContainsKey("Old map"))
                    {
                        Narrate("There is an old map on the floor.");
                        Narrate("Collect the old map?");
                        switch (Choose(Build("Yes, collect the map", "No, leave it")))
                        {
                            case 0: inventory.Add("Old map", 1); Narrate("You collect the old map."); break;
                            case 1: Narrate("You ignore the old map."); break;
                        }
                    }
                    Thread.Sleep(1000);
                    break;


                case 3: return;
            }

            AudioPlaybackEngine.Instance.StopLoopingMusic();
            AudioPlaybackEngine.Instance.PlayLoopingMusic("Fuzzball Parade.mp3");
        }


    }
    static void GregHouse()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Onion Capers.mp3");
        Person greg = new Person("Greg", ConsoleColor.Green, null);

        if (knowledge.Contains("Entered Greg's"))
        {
            greg.Say("What is it now?");
            if (knowledge.Contains("Scammed"))
            {
                Player.Say("You said the lady next door has a boat!");
                greg.Say("Yeah, not that she has it on her but she does own one.");
                Player.Say("Could you not have told me that earlier?");
                greg.Say("...", sleep: 100);
            }
            else
            {
                Player.Say("I was just wondering.");
                greg.Say("Well please wander elsewhere.");
            }
            Narrate("Greg closes the door on you.");
        }
        else
        {
            Player.Say("Is anyone home?");
            greg.Say("Ugh what is it now?");
            Player.Say("Wait--Greg?");
            greg.Say($"Oh not you again {Player.Name}!");
            Player.Say("How in the world did you get here?");
            greg.Say($"Well the cruise stopped on this island and then left without me.");
            greg.Say($"Now I'm trying to work out how to escape. The lady nextdoor owns a boat, but I don't have any money.");
            Player.Say("Hmm, well I'm gonna continue looking around, see you Greg!");
            greg.Say("Please don't come back.");
            knowledge.Add("Entered Greg's");
            Thread.Sleep(1000);
        }
    }
    static void ScammerHouse()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Music to Delight.mp3");

        Person chanelle = new Person("Chanelle", ConsoleColor.DarkMagenta, null);

        if (knowledge.Contains("Scammed"))
        {
            chanelle.Say("Sorry! I'm busy right now!");
        }
        else
        {
            if (!knowledge.Contains("Spoken with scammer"))
            {
                Player.Say("Hello?");
                chanelle.Say("Hi!");
                Player.Say("I heard you have a boat.");
                chanelle.Say("I heard you have a §(9)diamond§(5).");
                Player.Say("Well can I see the boat?");
                chanelle.Say("I'd like to see that §(9)diamond§(5).");

                knowledge.Add("Spoken with scammer");
            }
            else
            {
                Player.Say("Hello?");
                chanelle.Say("Back so soon?");
                chanelle.Say("How about that trade then?");
            }

            Narrate("Exchange the diamond with Chanelle?");
            switch (Choose(new ConsoleMessage[] { "Yes, exchange it!", "No, keep it!" }))
            {
                case 0:
                    chanelle.Say("OMGOODNESS! Thank you so much!");
                    Player.Say("So how about that boat?");
                    chanelle.Say("Uh, well I don't have it on me right now...");
                    chanelle.Say("But I will have it soon. Then you can... Use it.");
                    Narrate("You are too stunned to speak.");
                    Narrate("Chanelle closes the door on you.");
                    Player.Say("I can't help but think I got scammed...");
                    knowledge.Add("Scammed");
                    break;

                case 1:
                    Narrate("You choose to hold onto your diamond.");
                    chanelle.Say("Aw that's a real same. If you ever change your mind just come back!");
                    Narrate("You leave the house.");
                    break;
            }

            Thread.Sleep(1000);
        }
    }
    static void Shop()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Local Forecast.mp3");

        Person mackenzie = new Person("Mackenzie", ConsoleColor.DarkRed, null);

        mackenzie.Say($"Hey, {Player.Name}, welcome to my shop!");
        if (!knowledge.Contains("Spoken with shopkeeper"))
        {
            Player.Say($"I have §(14){inventory["Gold"]} gold§(11).");
            mackenzie.Say("And what about that §(9)diamond§(4)?");
            if (knowledge.Contains("Scammed"))
            {
                Player.Say("Chanelle took it from me!");
                Player.Say("She said she'd give me a boat in return and then kicked me out!");
                mackenzie.Say("Oh you can't trust that Chanelle.");
                mackenzie.Say("If you're lookign to get off this island, you'll be needing §(13)supplies §(4)from me!");
            }
            else
            {
                Player.Say("Oh yeah, I have that too.");
                mackenzie.Say("Well what are you looking for?");
                Player.Say("Is there any way to get off this island quickly?");
                mackenzie.Say("Oh yes, for sure. All you'll need is a §(13)boat§(4), a §(13)map§(4), and you can get §(13)supplies §(4)from me!");
            }
        }
        else
        {
            mackenzie.Say("You'll need a §(13)boat§(4), a §(13)map§(4), and you can get §(13)supplies §(4)from me!");
        }

        var shop = BuildShelves(NewShelf(NewItem("Bunch o' bananas", 5), NewItem("Teddy bear", 10), NewItem("Pencil", 3)),
                                NewShelf(NewItem("Roblox gift card", 100), NewItem("Paper", 3), NewItem("Rope", 5)));

        (int shelfI, int itemI) = ShopMenu(shop);
        string item = shop[shelfI][itemI].Item1.Contents;
        int cost = shop[shelfI][itemI].Item2;

        if (shelfI != -1)
        {

            if (ItemCount("Gold") < cost)
            {
                mackenzie.Say($"Sorry, you only have §(14){ItemCount("Gold")} gold§(4).");
                mackenzie.Say($"The {item} costs §(14){cost} gold§(4).");
                // earn money
            }
            else
            {
                mackenzie.Say($"Are you sure you would like to buy {item} for §(14){cost} gold§(4)?");
                switch (Choose(Build($"Yes, buy the {item}", "No, I don't want it")))
                {
                    case 0: Buy(cost); AddToInventory(item, 1); break;
                }
            }

            Narrate("§(8)Press any key to exit the shop.");
            Console.ReadKey();
        }

        AudioPlaybackEngine.Instance.PlayLoopingMusic("Fuzzball Parade.mp3");
    }
    static Tuple<int, int> ShopMenu(params Tuple<ConsoleMessage, int>[][] shelves)
    {
        Console.CursorVisible = false;
        ClearKeyBuffer();
        const int SpaceBetweenShelves = 2; // height = 2x width in the console so this is really 4 spaces which is a lot.
        const int SpaceBetweenItems = 10;
        int shelvesHeight = shelves.Sum(s => 3 + SpaceBetweenShelves);
        int totalItems = 0;
        int selectedShelf = 0;
        int selectedItem = 0;

        bool chosen = false;
        while (!chosen)
        {
            int yPos = (Console.WindowHeight - shelvesHeight) / 2;

            for (int i = 0; i < shelves.Length; i++)
            {
                Tuple<ConsoleMessage, int>[] shelf = shelves[i];
                int shelfLength = shelf.Sum(s => s.Item1.Length() + 8 + s.Item2.ToString().Length + SpaceBetweenItems); // const 8: taking into account the length of " - 100 gold"
                int xPos = (Console.WindowWidth - shelfLength) / 2;

                for (int j = 0; j < shelf.Length; j++)
                {
                    Tuple<ConsoleMessage, int> item = shelf[j];

                    ConsoleMessage content = $"{item.Item1.Contents} - §(14){item.Item2} gold";

                    ConsoleColor color = ConsoleColor.White;
                    ConsoleColor highlight = ConsoleColor.Black;

                    Console.ForegroundColor = color;
                    Console.BackgroundColor = highlight;
                    Console.SetCursorPosition(xPos, yPos);

                    Console.Write(new String('-', content.Length() + 4));
                    Console.SetCursorPosition(xPos, yPos + 1);
                    Console.Write("| ");

                    if (selectedShelf == i && selectedItem == j)
                    {
                        color = ConsoleColor.Yellow;
                        highlight = ConsoleColor.DarkGray;
                    }
                    else
                    {
                        color = item.Item1.Color;
                    }

                    Print(content.Contents, 0, highlight: highlight, initColor: color, logMessage: false);

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.Black;

                    Console.Write(" |");
                    Console.SetCursorPosition(xPos, yPos + 2);
                    Console.Write(new String('-', content.Length() + 4));

                    totalItems++;
                    xPos += content.Length() + 4 + SpaceBetweenItems;
                }

                yPos += 3 + SpaceBetweenShelves;
            }

            switch (Console.ReadKey(true).Key)
            {
                case ConsoleKey.RightArrow:
                    if (selectedItem < shelves[selectedShelf].Count() - 1)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        selectedItem++;
                        MainConsole.Refresh();
                    }; break;
                case ConsoleKey.LeftArrow:
                    if (selectedItem > 0)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        selectedItem--;
                        MainConsole.Refresh();
                    }
                    break;
                case ConsoleKey.DownArrow:
                    if (selectedShelf < shelves.Count() - 1)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        selectedShelf++;
                        MainConsole.Refresh();
                    } break;
                case ConsoleKey.UpArrow:
                    if (selectedShelf > 0)
                    {
                        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
                        selectedShelf--;
                        MainConsole.Refresh();
                    }
                    break;


                case ConsoleKey.Spacebar:
                case ConsoleKey.Enter:
                    AudioPlaybackEngine.Instance.PlaySound(menuSelect);
                    chosen = true;
                    break;
                case ConsoleKey.Escape:
                    AudioPlaybackEngine.Instance.PlaySound(pauseSound);
                    selectedShelf = -1; selectedItem = -1;
                    chosen = true; break;
            }
        }

        Console.CursorVisible = true;
        MainConsole.Refresh();

        return new Tuple<int, int>(selectedShelf, selectedItem);
    }
    static void Hotel()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Porch Swing Days Loop.mp3");

        Person shaniece = new Person("Shaniece", ConsoleColor.DarkGreen, null);

        if (ItemCount("Hotel Pass") == 0)
        {
            shaniece.Say("Welcome to my hotel.");
            shaniece.Say("Would you like to book a room?");
            shaniece.Say("It's 25 gold for one night.");
            Narrate("Book the night for 25 gold?");
            switch (Choose(Build("Yes, book the night", "No, not tonight")))
            {
                case 0:
                    int money = Buy(25);
                    if (money > 0) { Player.Say($"Oh, nevermind. I only have {money} gold."); }
                    else { shaniece.Say("Thank you. Come in whenever you like."); AddToInventory("Hotel Pass", 1); }
                    break;
                case 1: shaniece.Say("Come again soon!"); break;
            }
        }
        else
        {
            shaniece.Say($"Would you like to check in for the night, §(11){Player.Name}§(2)?");
            switch (Choose(Build("Yes, stay the night", "No, come back later")))
            {
                case 0:
                    Narrate("You go to bed.");
                    Narrate("Getting a good nights sleep helps you feel better");
                    RestoreCharisma(); break;
            }
        }

        Thread.Sleep(1000);
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Fuzzball Parade.mp3");
    }


    /// Monkey route
    static void UpdateMap(int[,] map, int playerDestination, int m1destination, int m2destination)
    {
        if (playerDestination != 16) // Don't update previous position if previous is out of bounds
        {
            map[playerDestination + 1, 4] = map[playerDestination + 1, 5];
        }
        map[playerDestination, 4] = 4;
        map[m1destination + 1, 2] = map[m1destination + 1, 5];
        map[m1destination, 2] = 3;
        map[m2destination + 1, 6] = map[m2destination + 1, 5]; // reset previous position
        map[m2destination, 6] = 3;                             // update new position
    }
    static void DrawMonkeyRace(int[,] map)
    {
        MainConsole.Refresh();
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, 5);

        for (int row = 0; row < 17; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                Console.ForegroundColor = ConsoleColor.White;
                switch (map[row, col])
                {
                    case 0:
                        Console.Write(" "); break;
                    case 1:
                        Console.Write("-"); break;
                    case 2:
                        Console.Write("#"); break;
                    case 3:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("☻"); break;
                    case 4:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("☻"); break;
                }
            }
            Console.WriteLine("");
        }
    }
    static void MonkeyRace()
    {
        MainConsole.Clear();
        // finish, 10 sand, start, 5 empty space
        int[,] map = new int[17, 9] {
            { 1, 1, 1, 1, 1, 1, 1, 1, 1 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            { 1, 1, 1, 1, 1, 1, 1, 1, 1 },
            { 0, 0, 3, 0, 0, 0, 3, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 4, 0, 0, 0, 0 } };
        int playerPos = 16;
        int monkeyPos1 = 12;
        int monkeyPos2 = 12;
        Print("After the countdown you will have 10 seconds to sprint to the finish line.");
        Print("Press [SPACE] as fast as you can to run faster.");
        Print("Walk to the start line to begin the race.");

        while (playerPos > 12)
        {
            DrawMonkeyRace(map);
            bool pressedSpace = false;
            while (!pressedSpace)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Spacebar) { pressedSpace = true; }
            }
            map[playerPos, 4] = 0;
            playerPos--;
            map[playerPos, 4] = 4;
            /*
            map = UpdateMap(map, playerPos, monkeyPos1, monkeyPos2);
            */
        }

        DrawMonkeyRace(map);
        CachedSound countdown = new CachedSound("RaceCountdown.wav");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlaySound(countdown);
        Print("§(4)3 ", 0); Thread.Sleep(1000);
        Print("§(4)2 ", 0); Thread.Sleep(1000);
        Print("§(4)1 ", 0); Thread.Sleep(1000);
        Print("§(10)GO! ", 2);
        ClearKeyBuffer();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Adventures Of Captian Trillian Loop.mp3");
        while (playerPos > 0)
        {
            MainConsole.Refresh(); // deletes everything, then rewrites only the logged messages.
            DrawMonkeyRace(map);
            bool pressedSpace = false;
            while (!pressedSpace)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Spacebar) { pressedSpace = true; }
            }
            playerPos--;
            int luck = rand.Next(0, 101);
            if (luck < 70) { monkeyPos1--; monkeyPos2--; }
            else if (luck < 80) { monkeyPos1--; }
            else if (luck < 90) { monkeyPos2--; }
            // else no monkeys move, good for you!
            UpdateMap(map, playerPos, monkeyPos1, monkeyPos2);
        }

        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        Print("§(10)FINISH!", y: 20);

        if (monkeyPos1 == 0 || monkeyPos2 == 0)
        {
            Narrate("You lost...");
            Narrate("The monkeys ransack all your belongings.");
            AddCharisma(-50);
            inventory.Clear();
            throw new NewGameException();
        }
    }
    static void MonkeyBattle()
    {
        // creativity is hard sometimes, this whole battle idea is stolen from Monkey Island (hence the monkeys)

        AudioPlaybackEngine.Instance.PlayLoopingMusic("Five Armies.mp3");
        Person king = new Person("King Monkey", ConsoleColor.DarkRed, null);

        king.Say("So, you--a meer human--think you can defeat me?");
        Player.Say("Well I was actu-");
        king.Say("SILENCE!", sleep: 20);
        Thread.Sleep(500);
        king.Say("You will battle me, and then we shall see who has the stronger wit!");
        // battle music
        int roundsWon = 0;
        king.Say("Nobody's ever drawn blood from me and nobody ever will.");
        var responses = new ConsoleMessage[] { "Oh yeah?", "I know you are but what am I?", "You run THAT fast?" };
        int response = SimpleChoose(responses);
        Player.Say(responses[response].Contents);
        if (response == 2) { king.Say("Uh... Be quiet!", 2); roundsWon++; }
        else { king.Say("Ha! Pitiful.", 2); }

        king.Say("Have you stopped wearing diapers yet?");
        responses = new ConsoleMessage[] { "You run THAT fast?", "Why? Did you want to borrow one?", "Oh yeah?" };
        response = SimpleChoose(responses);
        Player.Say(responses[response].Contents);
        if (response == 1) { king.Say("No! I didn't...", 2); roundsWon++; }
        else { king.Say("Disappointing.", 2); }

        king.Say("You're no match for my brains, you poor fool.");
        responses = new ConsoleMessage[] { "You run THAT fast?", "Why? Did you want to borrow one?", "I'd be in real trouble if you ever used them." };
        response = SimpleChoose(responses);
        Player.Say(responses[response].Contents);
        if (response == 2) { king.Say("Hey! That's not fair, you shouldn't know all of these.", 2); roundsWon++; }
        else { king.Say("A laughable response, but you're not funny.", 2); }

        king.Say("I got this scar on my face during a mighty struggle!");
        responses = new ConsoleMessage[] { "I hope now you've learned to stop picking your nose.", "Why? Did you want to borrow one?", "Oh yeah?" };
        response = SimpleChoose(responses);
        Player.Say(responses[response].Contents);
        if (response != 0 || roundsWon != 3)
        {
            king.Say("You're not even worth my time.", 2);
            king.Say("Guards, get this human out of my sight!");
            AudioPlaybackEngine.Instance.StopLoopingMusic();
            throw new NewGameException();
        }
        else
        {
            king.Say("Ugh, cheater! Nobody beats me!", 2);
            king.Say("Guards, take him to the volcano!", 2);
            Player.Say("YOU WHA");
            Console.Clear();
            AudioPlaybackEngine.Instance.StopLoopingMusic();
            Volcano();
        }
    }
    static void Volcano()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Clash Defiant.mp3");

        Person craig = new Person("Craig The Monkey", ConsoleColor.DarkGreen, null);
        Person walt = new Person("Walt The Monkey", ConsoleColor.DarkBlue, null);

        craig.Say("Ooo-oo Aa-aa! (Throw him into the volcano!)");
        walt.Say("Ooo-oo Eee-ee! (If he is truly the king he shall live!)");
        Player.Say("Nononono- wait we can talk about this!");
        craig.Say("Eee-ee Aa-aa! (You will live if you are king!)");
        Player.Say("HELP ME!");

        AudioPlaybackEngine.Instance.PlaySound(new CachedSound("CartoonFall.wav"));
        // fallign down sound
        Thread.Sleep(3000);
        ClearKeyBuffer();
        AudioPlaybackEngine.Instance.PauseLoopingMusic();
        Print("§(12)GRAB ONTO THE LEDGE WITH [SPACE]!");
        Thread.Sleep(500);
        if (!Console.KeyAvailable)
        {
            ClearKeyBuffer();
            MainConsole.Clear();
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound("Crash.mp3"), true);
            AudioPlaybackEngine.Instance.ResumeLoopingMusic();
            CenterText(new ConsoleMessage[] { "You died..." });
            throw new NewGameException();
        }
        AudioPlaybackEngine.Instance.StopAllSounds();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Boogie Party.mp3");
        ClearKeyBuffer();
        Narrate("In the nick of time you grabbed a ledge while you fell!");
        Narrate("The monkeys promote you to their new king.");
        throw new WonGameException();
    }
    static void Route1()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Jaunty Gumption.mp3");

        Person craig = new Person("Craig The Monkey", ConsoleColor.DarkGreen, null);
        Person walt = new Person("Walt The Monkey", ConsoleColor.DarkBlue, null);

        bool playGame = true;
        Narrate("A group of monkeys approaches!");
        Narrate("They challenge you to a race across the island.");
        Narrate("Do you accept?");
        if (Choose(Build("Yes, race the monkeys", "No, I have better things to do")) == 1)
        {
            MainConsole.Clear();
            Narrate("The monkeys inch closer towards you.");
            Narrate("You feel an unsettling atmosphere surround you.");
            Narrate("Do you accept?");
            if (Choose(Build("Yes, race the monkeys", "No, not right now")) == 1)
            {
                MainConsole.Clear();
                Narrate("The monkeys inch closer still.");
                Narrate("Do you accept?");
                if (Choose(Build("Yes, race the monkeys", "No, the monkeys can play with each other")) == 1)
                {
                    MainConsole.Clear();
                    Narrate("The monkeys give you one last chance to accept.");
                    Narrate("Do you accept?");
                    if (Choose(Build("Yes, race the monkeys", "No, ignore their warnings")) == 1)
                    {
                        AudioPlaybackEngine.Instance.StopLoopingMusic();
                        MainConsole.Clear();
                        craig.Say("Ooo-oo Aa-aa. (The human refuses to play.)");
                        walt.Say("Eee-ee Aaa-aa. (The human must battle our king.)");
                        playGame = false;
                    }
                }
            }
        }

        if (playGame)
        {
            MonkeyRace();
            MainConsole.Clear();
            AudioPlaybackEngine.Instance.PlayLoopingMusic("Winner Winner.mp3");

            craig.Say("Ooo-oo Aa-aa! (The human beat us!)");
            walt.Say("Aaa-aa Eee-ee! (The human must be our new king!)");
            craig.Say("Eee-ee Aa-aa! (We shall take the new king home!)");
            walt.Say("Aaa-aa Ooo-oo! (The human will battle for the throne!)");
        }

        Player.Say("Wait-wha");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        MonkeyBattle();
    }


    /// Pirate Route
    static void Route2()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Modern Jazz Samba.mp3");

        Person captain = new Person("Captain x", ConsoleColor.DarkRed, null);
        Person carl = new Person("Carl", ConsoleColor.DarkGreen, null);

        Narrate("A pirate ship approches!");
        captain.Say("LAND HO!");
        captain.Say("Ahoy, me hearties!");
        captain.Say("Ay, a bumbling scallywag!");
        Player.Say($"The name's, {Player.Name}!");
        captain.Say("That's not a very piratey name.");
        Player.Say("That's because I don't play pirates anymore.");
        captain.Say("Crew, capture this bafoon!");

        Player.Say("Oh for pity sake.");
        Narrate("The crew drag you onto their ship, and tie you up.");
        Narrate("They proceed to pillage the island.");

        Thread.Sleep(1000);
        CenterText(Build("Days pass..."), time: 2000, audioLocation: "DaysPass.wav");

        Narrate("The crew return, rather empty-handed, bar a couple bananas.");
        captain.Say("Me hearties, this island sucks.");
        captain.Say("Somepirate get this landlubber doing something useful!");
        Narrate("The ship sails away from the island.");

        Thread.Sleep(1000);
        CenterText(Build("Days pass..."), time: 2000, audioLocation: "DaysPass.wav");

        AudioPlaybackEngine.Instance.PlayLoopingMusic("Captain Scurvy.mp3");

        captain.Say("SCRUB THE DECKS!");

        captain.Say("CLIMB THE RIGGINGS!");
    }


    static void ResultsScreen()
    {
        AudioPlaybackEngine.Instance.StopLoopingMusic();

        // results roundup yay
    }
    public class NewGameException : Exception { }
    static void Main(string[] args)
    {
        bool stillPlaying = true;
        while (stillPlaying)
        {
            try
            {
                NewGame();
            }
            catch (NewGameException)
            {
                AudioPlaybackEngine.Instance.StopLoopingMusic();
            }
            catch (WonGameException)
            {
                stillPlaying = false;
            }
        }

        ResultsScreen();
    }

    /* OHMYGOODNESSGRACIOUSME a main method in ONLY 1 LINE WITH ONLY 3 SPACES AND ONLY 4 WORDS AND ONLY 179 CHARACTERS?!?!?!???!
     * 
     * static void Main(){bool p=true;while(p){try{NewGame();}catch(NewGameException){AudioPlaybackEngine.Instance.StopLoopingMusic();}catch(WonGameException){p=false;}}ResultsScreen();}
     */
}