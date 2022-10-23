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
        public ConsoleMessage(string contents, int newLines, ConsoleColor color, ConsoleColor highlight, int xVal, int yVal): this(contents, color)
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
        public ConsoleMessage() // only exists for temp placeholders with a message that can't exist
        {                       // (see ConsoleLogs.Remove())
            this.XVal = -1;
        }

        public static implicit operator ConsoleMessage(string s) => new ConsoleMessage(s);
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
        public static void Write(string contents, int newLines, ConsoleColor color, ConsoleColor highlight, int x, int y, int sleep, CachedSound? voice)
        {
            // -1 x & y is default code for current cursor position.
            if (x == -1) { x = Console.CursorLeft; }
            if (y == -1) { y = Console.CursorTop; }

            // Log the chat message so it can be re-written if the chat is updated or reset
            Log(new ConsoleMessage(contents, newLines, color, highlight, x, y));

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
    public static void Print(string contents, int newLines = 1, int x = -1, int y = -1, ConsoleColor highlight = ConsoleColor.Black, int sleep = -1, ConsoleColor initColor = ConsoleColor.White, CachedSound? voice = null)
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
                if (i == texts.Length - 1) { MainConsole.Write(texts[i], newLines, color, highlight, x, y, sleep, voice); }
                else { MainConsole.Write(texts[i], 0, color, highlight, x, y, sleep, voice); }
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

        MainConsole.Clear();
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
            MainConsole.Write(Name, 0, Color, highlight, x, y, sleep, Voice);
            contents = contents.Insert(0, ": ");

            Print(contents, newLines, x, y, highlight, sleep, Color, Voice);
        }

    }

    // [0] Fun | [1] Charisma | [2] BaseAttackDamage | [3] PuppiesSaved | [4] BladesOfGrassTouched
    static int[] stats = new int[5] { 0, 90, 0, 0, 0 };
    static Dictionary<string, int> inventory = new Dictionary<string, int> { { "Gold", 25 } };
    static Random rand = new Random();
    static CachedSound menuBleep = new CachedSound("MenuBleepSoft.wav");
    static CachedSound menuSelect = new CachedSound("MenuSelected.wav");
    static CachedSound pauseSound = new CachedSound("PauseButton.wav");
    
    public class CharismaZeroException : Exception { }
    public class WonGameException : Exception { }

    static Person Player = new Person("You", ConsoleColor.Cyan, null); // "YouSpeak.wav"

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
                Narrate("§(12)Your charisma is critically low!");
            }
        }
        else
        {
            stats[1] = newCharisma;
        }        
    }
    static void Instructions()
    {
        MainConsole.Clear();
        DigArt(0, 0);
        var messages = new ConsoleMessage[] { "Survive being cast away on a desert island!",
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
        Prologue();

        while (Console.KeyAvailable) { Console.ReadKey(false); } // clear consolekey buffer
        MainConsole.Clear();

        int route = Chapter1();
        
        while (Console.KeyAvailable) { Console.ReadKey(false); } // clear consolekey buffer
        MainConsole.Clear();

        switch (route)
        {
            case 0: Route0(); break;
            case 1: Route1(); break;
            case 2: Route2(); break;
        }
    }


    static void Prologue()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Night in venice.mp3");
        Person dave = new Person("Dave", ConsoleColor.Green, null); // "DaveSpeak.wav"
        Person greg = new Person("Greg", ConsoleColor.DarkCyan, null); // "GregSpeak.wav"

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
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Crickets.wav");
        AudioPlaybackEngine.Instance.PlaySound(new CachedSound("AwkwardCough.wav"));
        greg.Voice = new CachedSound("GregSpeak.wav");
        dave.Voice = new CachedSound("DaveSpeak.wav");
        Thread.Sleep(1000);
        greg.Say("...", sleep: 200);
        dave.Say("...", sleep: 200);
        greg.Voice = null;
        dave.Voice = null;
        Thread.Sleep(1000);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("OhNo.mp3");
        dave.Say($"LOL! Just kidding! Bye, {Player.Name}!");
        Narrate("Dave pushes you off the cruise, into the unforgiving waters.");
        Narrate("You fall.");
        Narrate("You survive.");
        Narrate("With a minor major interior exterior concussion.");
        Thread.Sleep(1500);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        CenterText(new ConsoleMessage[] { "Days pass..." }, time: 2000, audioLocation: "DaysPass.wav");
        MainConsole.Clear();
    }
    static int Chapter1()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("BeachSounds.wav");
        Thread.Sleep(1000);
        Narrate("You awake.");
        Player.Say("Uhh... what happened?");
        Player.Say("Wait, what? Am I on a deserted island?");
        Player.Say("Oh that §(10)Dave§(11)! What a prankster!");

        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Moonlight beach.mp3");

        ConsoleMessage[] choices = new ConsoleMessage[] { "Forage for food", "Dig for gold", "Shout for help", "Sleep", "View stats" };

        int route = -1;
        int loops = 0;
        while (route == -1)
        {
            if (loops == 5)
            {
                return rand.Next(1,3); // go down pirate or monkey route
            }
            Player.Say("What in the world should I do now?", 2);

            while (Console.KeyAvailable) { Console.ReadKey(false); } // clear consolekey buffer
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
                    Narrate($"Your §(12)charisma §(7)drops by 5.");
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
    static int Shout(bool diamonds)
    {
        if (!diamonds)
        {
            Person dave = new Person("Dave", ConsoleColor.Green, "DaveSpeak.wav");
            Person greg = new Person("Greg", ConsoleColor.DarkCyan, "GregSpeak.wav");
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
                Player.Say("Dave you came back!");
                dave.Say("And I'll leave as soon as I came.");
                Player.Say("Wait! Please help me out! I'm in dire need of your assistance!");
                dave.Say("Ugh okay, here's a §(9)diamond.");
                Narrate("§(13)Maybe someone will be interested in this...");
                Narrate($"Your§(12) charisma §(7)is restored to 100.");
                AddCharisma(100);
                Player.Say("See you later Dave!");
                Narrate("Dave disappears round a corner.");
                Player.Say("I probably should've followed him...");
                Narrate($"Your§(12) charisma §(7)drops by 5.");
                AddCharisma(-5);
            }
            else
            {
                greg.Say($"Ugh what is it now, {Player.Name}.");
                Player.Say("Please help me get off this island Greg!");
                greg.Say("Ugh, fine. Get on the boat");
                Narrate("Greg takes you back home.");
                throw new WonGameException();
            }
        }
        else
        {
            Person chanelle = new Person("Chanelle", ConsoleColor.DarkMagenta, null);
            Person addison = new Person("Addison", ConsoleColor.DarkBlue, null);
            Person betsie = new Person("Betsie", ConsoleColor.DarkGreen, null);
            Person mackenzie = new Person("Mackenzie", ConsoleColor.DarkRed, null);
            
            Player.Say("I have diamonds!");
            Thread.Sleep(1000);
            Narrate("The paparazzi appear, asking for your diamonds.");
            chanelle.Say("OMGOODNESS you have DIAMONDS!?");
            addison.Say($"WOW, {Player.Name.ToUpper()}, I LOVE YOU SO MUCH!");
            betsie.Say($"You don't mind sharing do you?");
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
                //MainConsole.Clear(1, 7);
                //DrawDigGame(map, xChoice, yChoice);

                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.RightArrow:
                        if (xChoice < 4)
                        {
                            xChoice++;
                            DrawDigGame(map, xChoice, yChoice);
                            /*
                            Console.Clear();
                            MainConsole.Refresh();*/
                        }
                        break;
                    case ConsoleKey.LeftArrow:
                        if (xChoice > 0)
                        {
                            xChoice--;
                            DrawDigGame(map, xChoice, yChoice);
                            /*
                            Console.Clear();
                            MainConsole.Refresh();*/
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if (yChoice < 4)
                        {
                            yChoice++;
                            DrawDigGame(map, xChoice, yChoice);
                            /*
                            Console.Clear();
                            MainConsole.Refresh();*/
                        }
                        break;
                    case ConsoleKey.UpArrow:
                        if (yChoice > 0)
                        {
                            yChoice--;
                            DrawDigGame(map, xChoice, yChoice);
                            /*
                            Console.Clear();
                            MainConsole.Refresh();*/
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
                AddToInventory("Diamonds", 1);
                diamond = true;
                charisma = 50;

            }

            Print($"You found {count} {item}§(15)!", 1, 0, 8 + choices*2);
            if (diamond) { Narrate("§(13)Maybe someone will be interested in this..."); }
            Print($"§(7)Your §(12)charisma §(7)goes {(charisma < 0 ? "down" : "up")} by {Math.Abs(charisma)}.");
            AddCharisma(charisma);
        }

        Print($"You have 0 choices remaining.", 2, 0, 0);
        DrawDigGame(map, -1, -1);
        if (diamond) { Narrate("§(13)Maybe someone will be interested in this..."); }
        Narrate("Press any key to continue.", 0, 0, 18);
        while (Console.KeyAvailable) { Console.ReadKey(false); } // clear consolekey buffer
        Console.ReadKey();
        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
        MainConsole.Clear();
    }
    
    static void Route0() // Diamond route
    {
        Person chanelle = new Person("Chanelle", ConsoleColor.DarkMagenta, null);
        Person addison = new Person("Addison", ConsoleColor.DarkBlue, null);
        Person betsie = new Person("Betsie", ConsoleColor.DarkGreen, null);
        Person mackenzie = new Person("Mackenzie", ConsoleColor.DarkRed, null);

        chanelle.Say($"Welcome to our village, {Player.Name}!");
    }

    static void MonkeyRace()
    {
        var countdown = new CachedSound("RaceCountdown.wav");
        AudioPlaybackEngine.Instance.PlaySound(countdown, true);
        Print("§(4)3 ", 0); Thread.Sleep(1000);
        Print("§(4)2 ", 0); Thread.Sleep(1000);
        Print("§(§)1 ", 0); Thread.Sleep(1000);
        Print("§(10)GO! ", 0); Thread.Sleep(1000);


    }
    static void Route1() // Monkey route
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic("Juanty Gumption.mp3");
        Narrate("A group of monkeys approaches!");
        Narrate("They challenge you to a race across the island.");
        Narrate("Do you accept?");
        if (Choose(new ConsoleMessage[] { "Yes", "No" }) == 1)
        {
            MainConsole.Clear();
            Narrate("The monkeys inch closer towards you.");
            Narrate("You feel an unsettling atmosphere surround you.");
            Narrate("Do you accept?");
            if (Choose(new ConsoleMessage[] { "Yes", "No" }) == 1)
            {
                MainConsole.Clear();
                Narrate("The monkeys inch closer still.");
                Narrate("Do you accept?");
                if (Choose(new ConsoleMessage[] { "Yes", "No" }) == 1)
                {
                    MainConsole.Clear();
                    Narrate("The monkeys give you one last chance to accept.");
                    Narrate("Do you accept?");
                    if (Choose(new ConsoleMessage[] { "Yes", "No" }) == 1)
                    {
                        MainConsole.Clear();
                        // epic monkey boss battle.
                    }
                }
            }
        }

        MonkeyRace();


    }
    static void Route2() // Pirate route
    { }


    public class NewGameException : Exception { }
    static void Main(string[] args)
    {
        stats[0] = rand.Next(0,101); // random 'fun' value (stole that idea from undertale)

        AudioPlaybackEngine.Instance.PlayLoopingMusic("Screen Saver.mp3");

        bool usingStartMenu = true;
        while (usingStartMenu)
        {
            DigArt(0, 100);
            int choice = Choose(new ConsoleMessage[] { "New Game", "Instructions", "Exit" }, false, new CachedSound("GameStart.wav"));
            switch (choice)
            {
                case 0: usingStartMenu = false; break;
                case 1: Instructions(); break;
                case 2: Environment.Exit(0); break;
            }
        }

        AudioPlaybackEngine.Instance.StopLoopingMusic();

        while (true)
        {
            try
            {
                NewGame();
            }
            catch (NewGameException)
            {
                AudioPlaybackEngine.Instance.StopLoopingMusic();
                Player.Name = "You";
                stats[0] = rand.Next(0, 101);
            }
        }
    }
}