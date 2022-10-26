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
        public static ConsoleMessage[] Build(params ConsoleMessage[] messages)
        {
            return messages;
        }
        public static ConsoleMessage[] ConvertStringArray(string[] array)
        {
            ConsoleMessage[] newArray = new ConsoleMessage[array.Length];
            for (int i=0; i < array.Length; i++) { newArray[i] = array[i]; }
            return newArray;
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
    static string ReadChars(int maxChars = -1)
    {
        string output = string.Empty;
        bool complete = false;
        int startPoint = Console.CursorLeft; // so that cursor does not go beyond starting point of text
        int x = startPoint; int y = Console.CursorTop;

        ConsoleLogs input = new ConsoleLogs();

        if (maxChars < -1)
        {
            while (!complete)
            {
                Console.SetCursorPosition(x, y);
                ConsoleKeyInfo keyPressed = Console.ReadKey(true);
                try { (x, y) = HandleKeyPress(input, keyPressed, startPoint, x, y); }
                catch (EnterException) { complete = true; }
            }
        }
        else
        {
            for (int i = 0; i < maxChars; i++)
            {
                Console.SetCursorPosition(x, y);
                ConsoleKeyInfo keyPressed = Console.ReadKey(true);
                try { (x, y) = HandleKeyPress(input, keyPressed, startPoint, x, y); }
                catch (EnterException) { break; }
            }
        }

        foreach (ConsoleMessage message in input.History)
        {
            output += message.Contents;
        }

        return output;
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
    static int Choose(ConsoleMessage[] options, bool escapable = true, CachedSound? mainSelectSound = null, bool drawHealth = false)
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
            if (drawHealth) { DrawHealth(); } // for pirate battle minigame, needs to redraw on console clear

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
        if (drawHealth) { DrawHealth(); }
        return choice;
    }
    static int SimpleChoose(ConsoleMessage[] options, bool escapable = true, CachedSound? mainSelectSound = null, bool drawHealth = false)
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
            if (drawHealth) { DrawHealth(); }
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
        if (drawHealth) { DrawHealth(); }
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
                CachedSound sound = new CachedSound(@"Sounds\DigSand.wav");
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
    static DateTime gameStarted;
    static CachedSound menuBleep = new CachedSound(@"Sounds\MenuBleep.wav");
    static CachedSound menuSelect = new CachedSound(@"Sounds\MenuSelect.wav");
    static CachedSound pauseSound = new CachedSound(@"Sounds\PauseButton.wav");
    public class CharismaZeroException : Exception { }
    public class WonGameException : Exception
    {
        public int Route { get; }
        public WonGameException(int route)
        {
            this.Route = route;
        }

    }

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
        if (narrate) { Narrate($"§(7)Your §(12)charisma §(7){(charisma < 0 ? "drops" : "goes up")} by {Math.Abs(charisma)}."); }
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
        var messages = new ConsoleMessage[] { "Survive being cast away on a seeminly remote desert island!",
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
        string timeSoFar = $"{(DateTime.Now - gameStarted):hh\\:mm\\:ss}";
        Print("Statistics", 2);

        Print($"§(12)Charisma§(15): {stats[1]}");
        Print($"Time taken: {timeSoFar}");

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
        MainConsole.Clear();
        stats[0] = rand.Next(0, 101); // random 'fun' value (stole that idea from undertale)
        stats[1] = 90; // Charisma level
        Player.Name = "You";
        inventory = new Dictionary<string, int> { { "Gold", 25 } };

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\00 Title Screen.mp3");

        bool usingStartMenu = true;
        while (usingStartMenu)
        {
            DigArt(0, 100);
            int choice = Choose(Build("New Game", "Instructions", "Exit"), false, new CachedSound(@"Sounds\GameStart.wav"));
            switch (choice)
            {
                case 0: usingStartMenu = false; break;
                case 1: Instructions(); break;
                case 2: Environment.Exit(0); break;
            }
        }

        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        gameStarted = DateTime.Now;

        
        Prologue();

        ClearKeyBuffer();
        MainConsole.Clear();

        int route = Chapter1();

        ClearKeyBuffer();
        MainConsole.Clear();

        switch (route)
        {
            case 0: Route1(); break;
            case 1: Route2(); break;
            case 2: Route3(); break;
        }
    }


    /// CHAPTER 1
    static void Prologue()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Night in venice.mp3");
        Person dave = new Person("Dave", ConsoleColor.DarkCyan, null); // "DaveSpeak.wav"
        Person greg = new Person("Greg", ConsoleColor.Green, null); // "GregSpeak.wav"

        MainConsole.Clear();
        Narrate("17:23. Ocean Atlantic Cruise", 2);
        dave.Say("Ha, good one Greg!");
        greg.Say("I know right, I'm a real comedian!");
        Player.Say("Um, actually that wasn't that funny...");
        greg.Say("What? Who's this kid?");
        Player.Say("Close your mouth! The name's ", 0);

        Player.Name = ReadStr();
        Print($"§(11){Player.Name}.");
        MainConsole.UpdateSpeaker("You", Player.Name);
        MainConsole.Refresh();

        greg.Say("What a ridiculous name.");
        dave.Say($"Hey, {Player.Name}'s my best friend!");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Crickets.wav");
        AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\AwkwardCough.wav"));
        greg.Voice = new CachedSound(@"Sounds\VoiceGreg.wav");
        dave.Voice = new CachedSound(@"Sounds\VoiceYou.wav");
        Thread.Sleep(1000);
        greg.Say("...", sleep: 200);
        dave.Say("...", sleep: 200);
        greg.Voice = null;
        dave.Voice = null;
        Thread.Sleep(1000);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\OhNo.mp3");
        dave.Say($"LOL! Just kidding! Bye, {Player.Name}!");
        Narrate("Dave pushes you off the cruise, into the unforgiving waters.");
        Narrate("You fall.");
        Narrate("You survive.");
        Narrate("With a minor major interior exterior concussion.");
        Thread.Sleep(1500);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        CenterText(Build("Days pass..."), time: 2000, audioLocation: @"Sounds\DaysPass.wav");
        MainConsole.Clear();
    }
    static int Chapter1()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Beach Sounds.wav");
        Thread.Sleep(1000);
        Narrate("You awake.");
        Player.Say("Uhh... what happened?");
        Player.Say("Wait, what? Am I on a deserted island?");
        Player.Say("Oh that §(3)Dave§(11)! What a prankster!");
        AudioPlaybackEngine.Instance.StopLoopingMusic();

        ConsoleMessage[] choices = Build("Forage for food", "Dig for gold", "Shout for help", "Sleep", "View stats");
        int route = -1;
        int loops = 0;

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Moonlight beach.mp3");
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
        AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\Discovery.wav"), true);
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
                throw new WonGameException(0);
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
        CachedSound sandDestroy = new CachedSound(@"Sounds\DigSand.wav");

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
    static void Route1()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Fuzzball Parade.mp3");

        Person chanelle = new Person("Chanelle", ConsoleColor.DarkMagenta, null);
        Person addison = new Person("Addison", ConsoleColor.DarkBlue, null);
        Person shaniece = new Person("Shaniece", ConsoleColor.DarkGreen, null);
        Person mackenzie = new Person("Mackenzie", ConsoleColor.DarkRed, null);

        chanelle.Say($"Welcome to our village, {Player.Name}!");
        addison.Say("Have a look around!");
        shaniece.Say("Take a look at my hotel for a place to sleep at night.");
        mackenzie.Say("Check out my shop to spend that §(9)diamond §(4)of yours.");

        ConsoleMessage[] options = Build("Mackenzie's Shop", "Shaniece's Hotel", "View stats", "Search Further East");
        bool skipRamble = false; // cleans the transition to the east side
        bool finished = false;
        while (!finished)
        {
            if (!skipRamble) { Player.Say("What do I do now?"); }
            skipRamble = false;
            switch (Choose(options))
            {
                case -1: MainMenu(); break;
                case 0: Shop(); break;
                case 1: Hotel(); break;
                case 2: ViewStats(); ViewInventory(); break;
                case 3: if (options[3] == (ConsoleMessage)"Search Further East")
                    {
                        if (EastSide())
                        {
                            options[3] = "§(13)Set sail";
                        };
                        skipRamble = true;
                    }
                    else
                    {
                        finished = true;
                    }
                    break;
            }

            if (!skipRamble) { MainConsole.Clear(); }
        }

        Narrate("You wander towards the boat to find a shambolic raft");
        Player.Say("Well I've not really got a choice, have I?");
        Narrate("You board the boat and set sail.");
        Thread.Sleep(1000);

        SailBoat();
    }
    static bool EastSide()
    {
        bool CanLeaveIsland = false;
        ConsoleMessage[] choices = { knowledge.Contains("Entered Greg's") ? "Greg's House" : "Small House",
                                     knowledge.Contains("Spoken with scammer") ? "Chanelle's House" : "Large House",
                                     "Sketchy Alleyway", "Return" };
        while (true)
        {
            int choice = Choose(choices);
            MainConsole.Refresh();
            switch (choice)
            {
                case 0: GregHouse(); choices[0] = "Greg's House"; break;
                case 1: CanLeaveIsland = ScammerHouse(); choices[1] = "Chanelle's House"; break;
                case 2: // sketchy allyway
                    AudioPlaybackEngine.Instance.StopLoopingMusic();
                    AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Cold Wind.mp3");
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

                case 3: return CanLeaveIsland;
            }

            AudioPlaybackEngine.Instance.StopLoopingMusic();
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Fuzzball Parade.mp3");
        }
    }
    static void GregHouse()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Onion Capers.mp3");
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
    static bool ScammerHouse()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Music to Delight.mp3");

        Person chanelle = new Person("Chanelle", ConsoleColor.DarkMagenta, null);

        if (knowledge.Contains("Scammed"))
        {
            if (ItemCount("Old map") > 0 && ItemCount("Rope") > 0)
            {
                chanelle.Say("Who is it?");
                Player.Say($"It's {Player.Name} again.");
                chanelle.Say("Ugh how badly do you want this boat?");
                Player.Say("I already payed for it!");
                chanelle.Say("Fine, fine, don't get your knickers in a twist.");
                chanelle.Say("I've docked it at the island bay. Take it! I don't want it.");
                Narrate("Chanelle shuts the door on you");
                return true;
            }

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
        return false;
    }
    static void Shop()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Local Forecast.mp3");

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

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Fuzzball Parade.mp3");
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
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Porch Swing Days Loop.mp3");

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
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Fuzzball Parade.mp3");
    }
    static void SailBoat()
    {

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
        Print("§(8)After the countdown you will have 10 seconds to sprint to the finish line.", Console.WindowHeight - 4);
        Print("§(8)Press [SPACE] as fast as you can to run faster.");
        Print("§(8)Walk to the start line to begin the race.");

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
        CachedSound countdown = new CachedSound(@"Sounds\RaceCountdown.wav");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlaySound(countdown);
        Print("§(4)3 ", 0); Thread.Sleep(1000);
        Print("§(4)2 ", 0); Thread.Sleep(1000);
        Print("§(4)1 ", 0); Thread.Sleep(1000);
        Print("§(10)GO! ", 2);
        ClearKeyBuffer();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Adventures Of Captian Trillian.mp3");
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Adventures Of Captian Trillian.mp3");
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

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Five Armies.mp3");
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
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Clash Defiant.mp3");

        Person craig = new Person("Craig The Monkey", ConsoleColor.DarkGreen, null);
        Person walt = new Person("Walt The Monkey", ConsoleColor.DarkBlue, null);

        craig.Say("Ooo-oo Aa-aa! (Throw him into the volcano!)");
        walt.Say("Ooo-oo Eee-ee! (If he is truly the king he shall live!)");
        Player.Say("Nononono- wait we can talk about this!");
        craig.Say("Eee-ee Aa-aa! (You will live if you are king!)");
        Player.Say("HELP ME!");

        AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\CartoonFall.wav"));
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
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\Crash.mp3"), true);
            AudioPlaybackEngine.Instance.ResumeLoopingMusic();
            CenterText(new ConsoleMessage[] { "You died..." });
            throw new NewGameException();
        }
        AudioPlaybackEngine.Instance.StopAllSounds();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Boogie Party.mp3");
        ClearKeyBuffer();
        Narrate("In the nick of time you grabbed a ledge while you fell!");
        Narrate("The monkeys promote you to their new king.");
        throw new WonGameException(2);
    }
    static void Route2()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Jaunty Gumption.mp3");

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
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Winner Winner.mp3");

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
    static void Route3Prologue()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Modern Jazz Samba.mp3");

        Person captain = new Person("Captain Blackbeard", ConsoleColor.DarkRed, null);
        Person carl = new Person("Stinkin' Carl", ConsoleColor.DarkGreen, null);

        Narrate("A pirate ship approches!");
        captain.Say("LAND HO!");
        captain.Say("Ahoy, me hearties!");
        carl.Say("Make way for the captain!");
        captain.Say("Ay, a bumbling scallywag!");
        Player.Say($"The name's {Player.Name}!");
        carl.Say("That's not a very piratey name.");
        Player.Say("That's because I don't play pirates anymore.");
        captain.Say("Crew, capture this bafoon!");

        Player.Say("Oh for pity sake.");
        Narrate("The crew drag you onto their ship, and tie you to the mast.");
        Narrate("They proceed to pillage the island.");

        Thread.Sleep(1000);
        AudioPlaybackEngine.Instance.PauseLoopingMusic();
        MainConsole.Clear();
        CenterText(Build("Days pass..."), time: 2000, audioLocation: @"Sounds\DaysPass.wav"); MainConsole.Clear();
        AudioPlaybackEngine.Instance.ResumeLoopingMusic();

        Narrate("The crew return, rather empty-handed, bar a couple bananas.");
        captain.Say("Me hearties, this island sucks.");
        captain.Say("Somepirate get this landlubber doing something useful!");
        Narrate("The ship sails away from the island.");

        Thread.Sleep(1000);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        CenterText(Build("Days pass..."), time: 2000, audioLocation: @"Sounds\DaysPass.wav"); MainConsole.Clear();
    }
    static void Route3()
    {
        //Route3Prologue();
        Person captain = new Person("Captain Blackbeard", ConsoleColor.DarkRed, null);
        Person carl = new Person("Stinkin' Carl", ConsoleColor.DarkGreen, null);

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Captain Scurvy.mp3");
        captain.Say("SCRUB THE DECK!");
        ScrubTheDeck();
        Console.SetCursorPosition(0, 1);
        captain.Say("Shiver me timbers!");
        Thread.Sleep(500);
        captain.Say("IS WHAT I WOULD SAY IF YE MATEYS WEREN'T SO SLOW!", sleep:30);
        Thread.Sleep(1000);

        captain.Say("NOW CLIMB THE RIGGING!");
        ClimbTheRigging();

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Modern Jazz Samba.mp3");
        Player.Name = "Cutlass " + Player.Name;

        captain.Say("Arggg, ye beat me fellow pirateers!");
        Player.Say("Blimey! Me lily-livered, bilge-sucking hearties are soon-to-be shark bait!");
        captain.Say($"Avast ye, crew! {Player.Name} reckons he can plunder all ye booties!");
        Player.Say("I didn't say that!");
        captain.Say("Well it be what I heard!");
        carl.Say("I'll fight 'em Cap'n!");
        captain.Say($"Well so it be! {carl.Name} shall battle {Player.Name}!");
        Player.Say("Oh no");

        if (PirateBattle())
        {
            MainConsole.Clear();
            captain.Say($"Ay, {Player.Name} ye truly were the stronger pirate.");
            Player.Say("ARG! I KNEW IT FROM THE START!");
            Narrate("You begin an unstoppable pirating career, collecting booty and tracking down treasure.");
            Narrate("Soon Captain Blackbeard retires and now resides in a care home.");
            Narrate("You, however, continued your pirating to your very last breath.");
            Thread.Sleep(1000);
            throw new WonGameException(2);
        }
        else
        {
            captain.Say("WALK THE PLANK YOU RAPSCALLION!");
            carl.Say("Dead men tell no tales!");
            Player.Say("Please! Don't make me do it!");
            captain.Say("Any last words?");
            Player.Say("");
            ReadChars(10);
            captain.Say("DONT REMEMBER ASKING!", sleep: 20);
            Thread.Sleep(500);
            Narrate("The captain shoves you onto the plank and forces you to walk.");
            Narrate("You walk the plank.");
            Narrate("You fall.");
            Narrate("You survive.");
            Narrate("With a minor major interior exterior concussion.");
            AddCharisma(-50);
            throw new NewGameException();
        }
    }
    static void DrawDeck(int[,] map)
    {
        MainConsole.Refresh();
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, 5);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(new String('-', 12));
        for (int row = 0; row < 6; row++)
        {
            Console.Write("|");
            for (int col = 0; col < 10; col++)
            {
                switch (map[row, col])
                {
                    case 0:
                        Console.Write(" "); break;
                    case 1:
                        Console.Write("#"); break;
                }
            }
            Console.WriteLine("|");
        }
        Console.Write(new String('-', 12));
    }
    static void ScrubTheDeck()
    {
        int[,] deck = new int[6, 10] { { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                                       { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0 },
                                       { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 },
                                       { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 },
                                       { 0, 0, 0, 0, 0, 0, 0, 1, 0, 0 },
                                       { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 } };

        DrawDeck(deck);
        Print("§(8)Press [SPACE] as fast as you can to scrub the dirt off the deck.", 1, 0, Console.WindowHeight - 3);

        for (int i = 5; i >= 0; i--)
        {
            MainConsole.Refresh(); // deletes everything, then rewrites only the logged messages.
            DrawDeck(deck);
            int spaceCount = 0;
            while (spaceCount < 5)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Spacebar) { spaceCount++; }
            }
            for (int j = 0; j < 9; j++)
            {
                deck[i, j] = 0;
            }
        }


    }
    static void UpdateRigging(int[,] map, int playerDestination, int p1destination, int p2destination)
    {
        // p1 is on rigging [0], player is on rigging [3], p2 is on rigging [6]
        int[] emptyRigging = new int[17] { 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1, 0, 0, 0, 0, 0 };

        if (playerDestination != 16) // Don't update previous position if previous is out of bounds       
        {
            map[playerDestination + 1, 3] = emptyRigging[playerDestination + 1];
        }
        map[playerDestination, 3] = 4;
        map[p1destination + 1, 0] = emptyRigging[p1destination + 1];
        map[p1destination, 0] = 3;
        map[p2destination + 1, 6] = emptyRigging[p2destination + 1]; // reset previous position
        map[p2destination, 6] = 3;                                  // update new position
    }
    static void DrawRigging(int[,] map)
    {
        MainConsole.Refresh();
        Console.CursorVisible = false;
        Console.SetCursorPosition(0, 5);

        for (int row = 0; row < 17; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                Console.ForegroundColor = ConsoleColor.White;
                switch (map[row, col])
                {
                    case 0:
                        Console.Write(" "); break;
                    case 1:
                        Console.Write("-"); break;
                    case 2:
                        Console.Write("|"); break;
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
    static void ClimbTheRigging()
    {
        MainConsole.Refresh();

        // finish, 10 rope, start, 5 empty space
        int[,] map = new int[17, 7] {
            { 1, 1, 1, 1, 1, 1, 1 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 2, 0, 0, 2, 0, 0, 2 },
            { 1, 1, 1, 1, 1, 1, 1 },
            { 3, 0, 0, 0, 0, 0, 3 },
            { 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 4, 0, 0, 0 } };

        int player = 16;
        int pirate1 = 12;
        int pirate2 = 12;

        Print("§(8)Press [SPACE] as fast as you can to climb faster that the other pirates.", 1, 0, Console.WindowHeight-3);
        Print("§(8)Walk to the start line to begin the race.");

        while (player > 12)
        {
            DrawRigging(map);
            bool pressedSpace = false;
            ClearKeyBuffer();
            while (!pressedSpace)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Spacebar) { pressedSpace = true; }
            }
            map[player, 3] = 0;
            player--;
            map[player, 3] = 4;
        }

        DrawRigging(map);
        CachedSound countdown = new CachedSound(@"Sounds\RaceCountdown.wav");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlaySound(countdown);
        Print("§(4)3 ", 0); Thread.Sleep(1000);
        Print("§(4)2 ", 0); Thread.Sleep(1000);
        Print("§(4)1 ", 0); Thread.Sleep(1000);
        Print("§(10)GO! ", 2);
        ClearKeyBuffer();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Adventures Of Captian Trillian.mp3");
        while (player > 0)
        {
            MainConsole.Refresh(); // deletes everything, then rewrites only the logged messages.
            DrawRigging(map);
            bool pressedSpace = false;
            while (!pressedSpace)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Spacebar) { pressedSpace = true; }
            }
            player--;
            int luck = rand.Next(0, 101);
            if (luck < 70) { pirate1--; pirate2--; }
            else if (luck < 80) { pirate1--; }
            else if (luck < 90) { pirate2--; }
            // else no priates move, lucky you!
            UpdateRigging(map, player, pirate1, pirate2);
        }

        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        
        if (pirate1 == 0 || pirate2 == 0)
        {
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Man down.mp3");
            Person captain = new Person("Captain Blackbeard", ConsoleColor.DarkRed, null);
            captain.Say($"Oi! {Player.Name} The other pirates beat you!");
            captain.Say("I always knew you were a lowlife slacker!");
            captain.Say("Crew, show the rapscallion what we do with slackers!");
            Narrate("The pirates ransack all your belongings.");
            Narrate("They make you walk the plank.");
            Narrate("You fall.");
            Narrate("You survive.");
            Narrate("With a minor major interior exterior concussion.");
            AddCharisma(-50);
            throw new NewGameException();
        }
    }

    // Pirate Battle
    static int carlHealth = 100;
    static int playerHealth = 100;
    static void AddToHealth(string person, int health)
    {
        if (person.ToLower() == "carl")
        {
            carlHealth = (carlHealth + health > 100) ? 100 : (carlHealth + health < 0) ? 0 : carlHealth + health;
            string sign = (health < 0) ? "-" : "+";
            string color = (health < 0) ? "10" : "12";
            Narrate($"§({color}){sign}{Math.Abs(health)} Health.", sleep: 55);
        }
        else
        {
            playerHealth = (playerHealth + health > 100) ? 100 : (playerHealth + health < 0) ? 0 : playerHealth + health;
            string sign = (health < 0) ? "-" : "+";
            string color = (health < 0) ? "12" : "10";
            Narrate($"§({color}){sign}{Math.Abs(health)} Health.", sleep: 55);
        }
        DrawHealth();
    }
    static void DrawHealth()
    {
        string longestName; string longestNameHealth; ConsoleColor longestNameColor;
        string shortestName; string shortestNameHealth; ConsoleColor shortestNameColor;

        if (Player.Name.Length > carl.Name.Length)
        {
            longestName = Player.Name;
            longestNameHealth = $"{new String(' ', 3 - playerHealth.ToString().Length)}{playerHealth}";
            longestNameColor = Player.Color;
            shortestName = carl.Name;
            shortestNameHealth = $"{new String(' ', 3 - carlHealth.ToString().Length)}{carlHealth}";
            shortestNameColor = carl.Color;
        }
        else
        {
            longestName = carl.Name;
            longestNameHealth = $"{new String(' ', 3 - carlHealth.ToString().Length)}{carlHealth}";
            longestNameColor = carl.Color;
            shortestName = Player.Name;
            shortestNameHealth = $"{new String(' ', 3 - playerHealth.ToString().Length)}{playerHealth}";
            shortestNameColor = Player.Color;
        }
        shortestName += new String(' ', longestName.Length - shortestName.Length);


        int resetX = Console.CursorLeft; int resetY = Console.CursorTop; //total width: 12 Player.Name.Length 345678
        int totalWidth = longestName.Length + 8;                         //             | ReallyLongPlayerName 100 |
        int xIndent = Console.WindowWidth - totalWidth;

        Console.ForegroundColor = ConsoleColor.White;
        Console.SetCursorPosition(xIndent, 0);
        Console.Write(new String('-', totalWidth));
        Console.SetCursorPosition(xIndent, 1);

        //Console.Write($"| {longestName} {longestNameHealth} |");
        Console.Write("| "); Console.ForegroundColor = longestNameColor;
        Console.Write($"{longestName} {longestNameHealth}"); Console.ForegroundColor = ConsoleColor.White; Console.Write(" |");
        Console.SetCursorPosition(xIndent, 2);

        //Console.Write($"| {shortestName} {shortestNameHealth} |");
        Console.Write("| "); Console.ForegroundColor = shortestNameColor;
        Console.Write($"{shortestName} {shortestNameHealth}"); Console.ForegroundColor = ConsoleColor.White; Console.Write(" |");
        Console.SetCursorPosition(xIndent, 3);
        Console.Write(new String('-', totalWidth));

        Console.SetCursorPosition(resetX, resetY);
    }

    static bool carlDistracted = false;
    static bool playerDistracted = false;
    static bool surrendered = false;
    static ConsoleMessage[]? weapons;
    static ConsoleMessage[] goodInsults = Build("You fight like a dairy farmer!", "I've spoken with apes more polite than you!", "I once owned a dog that was smarter than you.",
                                                "You're not invited to my birthday party!", "You're no match for my brains you poor fool.", "You have the manners of a beggar.");
    static ConsoleMessage[] badInsults = Build("You're as blind as a bat!", "You're as ugly as an elephant.", "Eveything you say is stupid!", "You probably can't even go cross-eyed!",
                                               "You coward!", "You make me sick!", "I'd like my steak chicken-fried.", "I bet your wardrobe isn't even color-coordinated!");
    static Person carl = new Person("Stinkin' Carl", ConsoleColor.DarkGreen, null);
    static bool PirateBattle()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Bushwick Tarantella.mp3");
        MainConsole.Clear();
        
        carl.Say("This is the end for you, you gutter-crawling cur!");
        weapons = ConvertStringArray(inventory.Keys.ToArray());
        DrawHealth();
        while (carlHealth > 1 && playerHealth > 1 && !surrendered)
        {
            if (playerDistracted)
            {
                Narrate("You are too distracted to make a move!", sleep:55);
                playerDistracted = false;
            }
            else
            {
                PlayerAttack();
            }

            if (carlHealth < 1 || playerHealth < 1 || surrendered) { break; }

            if (carlDistracted)
            {
                Narrate("Carl is too distracted to make a move!", sleep: 55);
                carlDistracted = false;
            }
            else
            {
                CarlAttack();
            }
        }

        AudioPlaybackEngine.Instance.StopLoopingMusic();

        if (surrendered)
        {
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Man down.mp3");
            carl.Say("Ahargh! I knew ye were a meer picaroon! Scupper that scoundrel!");
            Thread.Sleep(1000);
            return false;
        }
        else if (playerHealth < 1)
        {
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Man down.mp3");
            Player.Say("By golly gosh you've made me toast!");
            carl.Say("I always knew you weren't a real pirate!");
            Thread.Sleep(1000);
            return false;
        }
        else
        {
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Vivacity.mp3");
            carl.Say("Aye, I've been bested. Me booty be the Captains.");
            Narrate("Carl yields.");
            Thread.Sleep(1000);
            return true;
        }
    }
    static void PlayerAttack()
    {
        weapons ??= new ConsoleMessage[1]; // weapons should never be null at this point. This is for the sake of stopping it from crying about null dereferences. 
        switch (Choose(Build("Attack", "Use item", "Insult", "Surrender"), drawHealth: true))
        {
            case 0: int choice = Choose(weapons, drawHealth: true);
                if (choice == -1) { } // escape
                switch (weapons[choice].Contents) // attack
                {
                    case "Gold":
                        Narrate("You hurl a handful of gold coins at Carl.", sleep:55);
                        carl.Say("Ouch!");
                        AddToHealth("carl", -15);
                        break;
                    case "Sticks":
                        Narrate("You toss the stick at Carl.", sleep: 55);
                        carl.Say("Nice throw.");
                        AddToHealth("carl", -8);
                        break;
                    case "Coconuts":
                        Narrate("You lob a coconut at Carl.", sleep: 55);
                        carl.Say("ARGH!");
                        Narrate("§(12)CRITICAL HIT!", sleep: 55);
                        AddToHealth("carl", -10);
                        break;
                    case "Used TP":
                        AudioPlaybackEngine.Instance.PauseLoopingMusic();
                        Narrate("You apply the used toilet paper to Carl."); carl.Voice = new CachedSound(@"Sounds\VoiceDave.wav");
                        carl.Say("...", sleep: 150); carl.Voice = null;
                        Narrate("§(12)CRITICAL HIT!", sleep: 30);
                        AddToHealth("carl", -50);
                        AudioPlaybackEngine.Instance.ResumeLoopingMusic(); break;
                    case "Diamonds":
                        Narrate("You propel a priceless diamond at Carl.", sleep: 55);
                        carl.Say("Thanks!");
                        AddCharisma(-20);
                        AddToHealth("carl", 20);
                        break;
                } break;

            case 1:
                switch (weapons[Choose(weapons, drawHealth: true)].Contents) // use item
                {
                    case "Gold":
                        Narrate("You... Eat the gold?", sleep: 55);
                        Player.Say("Ouch!");
                        AddToHealth("player", -10);
                        break;
                    case "Sticks":
                        Narrate("You arrange the sticks into a pretty pattern.", sleep: 55);
                        Narrate("§(10)Carl is distracted for 1 round!", sleep:55);
                        carlDistracted = true; break;
                    case "Coconuts":
                        Narrate("You eat the coconut.", sleep:55);
                        Player.Say("Mmm! Delicious!");
                        AddToHealth("player", 30);
                        break;
                    case "Used TP":
                        AudioPlaybackEngine.Instance.PauseLoopingMusic();
                        Narrate("You... use the toilet paper."); carl.Voice = new CachedSound(@"Sounds\VoiceDave.wav");
                        carl.Say("...", sleep: 150); carl.Voice = null;
                        Narrate("Carl is disgusted.");
                        Narrate("§(10)Carl is distracted for 1 round!", sleep:55);
                        carlDistracted = true;
                        AudioPlaybackEngine.Instance.ResumeLoopingMusic(); break;
                    case "Diamonds":
                        Narrate("You stare at the diamond, amazed by it's reflectivity.", sleep:55);
                        Narrate("§(12)You are distracted for 1 round!", sleep:55);
                        playerDistracted = true; break;
                }
                break;

            case 2:
                ConsoleMessage[] insults = Build(goodInsults[rand.Next(0, goodInsults.Length)], badInsults[rand.Next(0, badInsults.Length)], badInsults[rand.Next(0, badInsults.Length)]);
                insults = insults.OrderBy(x => rand.Next()).ToArray(); // randomise order so correct isnt always first
                ConsoleMessage insult = insults[SimpleChoose(insults, drawHealth: true)];

                Player.Say(insult.Contents);

                if (goodInsults.Contains(insult))
                {
                    Narrate("Carl begins to cry. You hurt his feelings.", sleep: 55);
                    AddToHealth("carl", -10);
                }
                else
                {
                    Narrate("Your insult was so laughably bad that Carl's charisma went up.", sleep: 55);
                    AddToHealth("carl", 20);
                }
                break;

            case 3: surrendered = true; break;
        }
    }
    static void CarlAttack()
    {
        int choice = rand.Next(0, 101);
        if (choice < 50) // 50% chance enemy atttacks
        {
            switch(rand.Next(0, 4))
            {
                case 0: // sword
                    Narrate("Carl attempts to strike you with his sword.", sleep:55);
                    if (rand.Next(0,11) < 8) { Narrate("He missed."); }
                    else
                    {
                        Narrate("He hits! Inflicting a critical wound!", sleep:55);
                        AddToHealth("player", -50); // 50% attacks * 25% uses sword * 20% lands hit = 2.5% chance he hits you
                    } break;
                case 1: // gold
                    Narrate("Carl hurls a handful of gold coins at you.", sleep:55);
                    Player.Say("Ouch!"); 
                    AddToHealth("player", -10);
                    break;

                case 2: // banana
                    Narrate("Carl lobs a bunch of bananas at you.", sleep:55);
                    Player.Say("ARGH!"); 
                    Narrate("§(12)CRITICAL HIT!", sleep: 55);
                    AddToHealth("player", -30);
                    break;

                case 3: // canonball
                    Narrate("Carl attempts to lift a canonball to throw at you.", sleep: 55);
                    Narrate("The ball slips out of his hands, landing on his feet and stunning him.", sleep: 55);
                    Narrate("§(10)Carl is distracted for 1 round!", sleep:55);
                    carlDistracted = true; break;
            }
        }

        else if (choice < 75) // 25% chance he uses an item
        {
            switch (rand.Next(0, 3))
            {
                case 0: // banana
                    Narrate("Carl eats a banana.", sleep:55);
                    carl.Say("Mmm! Delicious!");
                    AddToHealth("carl", 20);
                    break;
                case 1: // banana
                    Narrate("Carl... Eats some gold coins?", sleep:55);
                    carl.Say("Ouch!");
                    AddToHealth("carl", -10);
                    break;
                case 2: // bandana
                    Narrate("Carl reveals from his pockets... a bandana.", sleep:55);
                    Narrate("It had a maths equation written on it. You can't stop yourself from solving it.", sleep:55);
                    Narrate("§(12)You are distracted for 1 round!", sleep:55);
                    playerDistracted = true; break;
            }
        }
        else // 25% chance he insults
        {
            string[] insults = new string[] { goodInsults[rand.Next(0, goodInsults.Length)].Contents, badInsults[rand.Next(0, badInsults.Length)].Contents, badInsults[rand.Next(0, badInsults.Length)].Contents };
            string insult = insults[rand.Next(0, 3)];

            carl.Say(insult);

            if (insult == insults[0]) // if carl chose good insult (33% chance)
            {
                Narrate("That one hit a little too close to home. You begin crying.", sleep:55);
                AddToHealth("player", -10);
            }
            else
            {
                Narrate("Carl's insult was so laughably bad that your charisma went up.", sleep:55);
                AddCharisma(20);
                AddToHealth("player", 20);
            }
        }
    }


    static void ResultsScreen(int route)
    {
        string totalTime = $"{(DateTime.Now - gameStarted):hh\\:mm\\:ss}";
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        if (route != -1) { AudioPlaybackEngine.Instance.PlayLoopingMusic(@$"Music\9{route} Results Screen.mp3"); }
        MainConsole.Clear();

        Print("Your Life as a Cast Away", 2);

        Narrate($"§(12)Final Charisma§(15): {stats[1]}");
        Narrate($"§(14)Gold collected§(15): 1");
        Narrate($"§(15)Time taken: {totalTime}");
        switch (route)
        {
            case 0:
                Narrate("§(15)Times fallen off a boat: 1");
                Narrate("§(15)Times saved by the bystander watching you get pushed off the boat: 1");
                break;
                
            case 1:
                Narrate($"§(15)Times been scammed: 1");
                // need more here
                break;
            case 2:
                Narrate($"§(15)Monkeys beaten in a insult battle: 1");
                Narrate($"§(15)Monkeys beaten in a race: 2");
                Narrate($"§(15)Volcanoes merely survived: 1");
                break;
            case 3:
                Narrate($"§(15)Pirates beaten in a battle: 1");
                Narrate($"§(15)Pirates beaten in a race: 2");
                Narrate($"§(15)Decks scrubbed: 1");
                break;
        }

        Print("", 1);
        Print("§(8)Press any key to exit.");
        Console.ReadKey();

        MainConsole.Clear();
    }
    public class NewGameException : Exception { }
    static void Main(string[] args)
    {
        bool stillPlaying = true;
        int route = -1;
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
            catch (WonGameException e)
            {
                stillPlaying = false;
                route = e.Route;
            }
        }

        ResultsScreen(route);
    }

    /* OHMYGOODNESSGRACIOUSME a main method in ONLY 1 LINE WITH ONLY 3 SPACES AND ONLY 4 WORDS AND ONLY 179 CHARACTERS?!?!?!???!
     * 
     * static void Main(){bool p=true;while(p){try{NewGame();}catch(NewGameException){AudioPlaybackEngine.Instance.StopLoopingMusic();}catch(WonGameException){p=false;}}ResultsScreen();}
     */
}