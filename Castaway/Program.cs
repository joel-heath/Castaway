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
using System.ComponentModel.DataAnnotations;

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
            for (int i = 0; i < array.Length; i++) { newArray[i] = array[i]; }
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

        if (maxChars == -1)
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
    static void DigArt(int artChoice, int speed = 0, int yIndent = 0)
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
            case 1: art = @"
                                  )___(                       
                           _______/__/_
                  ___     /===========|   ___
 ____       __   [\\\]___/____________|__[///]   __
 \   \_____[\\]__/___________________________\__[//]___
  \                                                    |
   \                                                  /
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"; break;


            case 2: art = @"
              ,.  _~-.,               .                                 
           ~'`_ \/,_. \_
          / ,''_>@`,__`~.)             |           .
          | |  @@@@'  '',! .           .          '
          |/   ^^@     .!  \          |         /
          `' .^^^     ,'    '         |        .             .
           .^^^   .          \                /          .
          .^^^       '  .     \       |      /       . '
.,.,.     ^^^             ` .   .,+~'`^`'~+,.     , '
&&&&&&,  ,^^^^.  . ._ ..__ _  .'             '. '_ __ ____ __ _ .. .  .
%%%%%%%%%^^^^^^%%&&;_,.-=~'`^`'~=-.,__,.-=~'`^`'~=-.,__,.-=~'`^`'~=-., 
&&&&&%%%%%%%%%%%%%%%%%%&&;,.-=~'`^`'~=-.,__,.-=~'`^`'~=-.,__,.-=~'`^`'~=
%%%%%&&&&&&&&&&&%%%%&&&_,.;^`'~=-.,__,.-=~'`^`'~=-.,__,.-=~'`^`'~=-.,__,
%%%%%%%%%&&&&&&&&&-=~'`^`'~=-.,__,.-=~'`^`'~=-.,__,.-==--^'~=-.,__,.-=~'
##########*'''
_,.-=~'`^`'~=-.,__,.-=~'`^`'~=-.,__,.-=~'`^`'~=-.,.-=~'`^`'~=-.,__,.-=~'

~`'^`'~=-.,__,.-=~'`^`'~=-.,__,.-=~'`^`'~=-.,__,.-=~'`^`'~=-.,__,.-=~'`^"; break;

            case 3: art = @"
                                                           |>>>         
                   _                      _                |
    ____________ .' '.    _____/----/-\ .' './========\   / \
   //// ////// /V_.-._\  |.-.-.|===| _ |-----| u    u |  /___\
  // /// // ///==\ u |.  || | ||===||||| |T| |   ||   | .| u |_ _ _ _ _ 
 ///////-\////====\==|:::::::::::::::::::::::::::::::::::|u u| U U U U U
 |----/\u |--|++++|..|'''''''''''::::::::::::::''''''''''|+++|+-+-+-+-+-
 |u u|u | |u ||||||..|              '::::::::'           |===|>=== _ _ =
 |===|  |u|==|++++|==|              .::::::::.           | T |....| V |.
 |u u|u | |u ||HH||         \|/    .::::::::::.
 |===|_.|u|_.|+HH+|_              .::::::::::::.              _
                __(_)___         .::::::::::::::.         ___(_)__
---------------/  / \  /|       .:::::;;;:::;;:::.       |\  / \  \-----
______________/_______/ |      .::::::;;:::::;;:::.      | \_______\____
|       |     [===  =] /|     .:::::;;;::::::;;;:::.     |\ [==  = ]   |
|_______|_____[ = == ]/ |    .:::::;;;:::::::;;;::::.    | \[ ===  ]___|
     |       |[  === ] /|   .:::::;;;::::::::;;;:::::.   |\ [=  ===] |
_____|_______|[== = =]/ |  .:::::;;;::::::::::;;;:::::.  | \[ ==  =]_|__
 |       |    [ == = ] /| .::::::;;:::::::::::;;;::::::. |\ [== == ]    
_|_______|____[=  == ]/ |.::::::;;:::::::::::::;;;::::::.| \[  === ]____
   |       |  [ === =] /.::::::;;::::::::::::::;;;:::::::.\ [===  =]   |
___|_______|__[ == ==]/.::::::;;;:::::::::::::::;;;:::::::.\[=  == ]___|"; break;



            case 5: art = @"
              |    |    |                 
             )_)  )_)  )_)              
            )___))___))___)\            
           )____)____)_____)\\
         _____|____|____|____\\\__
---------\                   /---------
  ^^^^^ ^^^^^^^^^^^^^^^^^^^^^
    ^^^^      ^^^^     ^^^    ^^
         ^^^^      ^^^"; break;

            case 6: art = @"
                        
     /
     \O        \
   _ /`--|)---- \_ O__/
   \`\          `\/_\
  `` /_            \\
                   ` \ 
                     -'"; break;
        }

        if (center)
        {
            using StringReader reader = new StringReader(art);
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
        else
        {
            int x = Console.CursorLeft; int y = Console.CursorTop;
            using StringReader reader = new StringReader(art);
            string? line;
            int xIndent = Console.WindowWidth - art.Split('\n')[1].Length;
            int yLevel = yIndent;
            while ((line = reader.ReadLine()) != null)
            {
                Print(line, 0, xIndent, yLevel++);
            }
            Console.SetCursorPosition(x, y);
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

    // [0] Fun | [1] Charisma 
    static int[] stats = new int[2];
    static List<string> knowledge = new List<string>(); // Used for storing whether player has entered a house and stuff
    static Dictionary<string, int> inventory = new Dictionary<string, int>();
    static Random rand = new Random();
    static DateTime gameStarted;
    static CachedSound menuBleep = new CachedSound(@"Sounds\MenuBleep.wav");
    static CachedSound menuSelect = new CachedSound(@"Sounds\MenuSelect.wav");
    static CachedSound pauseSound = new CachedSound(@"Sounds\PauseButton.wav");
    public class CharismaZeroException : Exception { }
    public class NewGameException : Exception { }
    public class ExitGameException : Exception { }
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
        if (inventory.ContainsKey(key))
        {
            int newValue = inventory[key] + value;
            if (newValue < 0) { inventory.Remove(key); return; }
            inventory[key] = newValue;
        }
        else
        {
            if (value < 0) { return; }
            inventory.Add(key, value);
        }
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
        var messages = new ConsoleMessage[] { "Survive being cast away on a seemingly remote desert island!",
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
        ClearKeyBuffer();
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
        ClearKeyBuffer();
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
        inventory = new Dictionary<string, int> { { "Gold", 30 } };

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
                case 2: throw new ExitGameException();
            }
        }

        AudioPlaybackEngine.Instance.StopLoopingMusic();
        MainConsole.Clear();
        gameStarted = DateTime.Now;

        Prologue();

        ClearKeyBuffer();
        MainConsole.Clear();

        int route = Chapter1();

        switch (route)
        {
            case 1: Route1(); break;
            case 2: Route2(); break;
            case 3: Route3(); break;
        }
    }


    /// CHAPTER 1
    static void Prologue()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Night in venice.mp3");
        Person dave = new Person("Dave", ConsoleColor.DarkCyan, null); // "DaveSpeak.wav"
        Person greg = new Person("Greg", ConsoleColor.Green, null); // "GregSpeak.wav"

        MainConsole.Clear();
        DigArt(2);
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
        DigArt(2);
        Thread.Sleep(1000);
        Narrate("You awake.");
        Player.Say("Uhh... what happened?");
        Player.Say("Wait, what? Am I on a deserted island?");
        Player.Say("Oh that Dave! What a prankster!");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Moonlight beach.mp3");

        ConsoleMessage[] choices = Build("Forage for food", "Dig for gold", "Shout for help", "Sleep", "View stats");
        int route = -1;
        int loops = 0;

        while (loops < 5)
        {
            DigArt(2);
            Player.Say("What in the world should I do now?", 2);

            ClearKeyBuffer();
            int choice = Choose(choices);
            switch (choice)
            {
                case -1: MainMenu(); break;
                case 0: Forage(); break;
                case 1: DigGame(); break;
                case 2: Shout(); break;
                case 3:
                    Narrate("You lay down and try to sleep.");
                    Narrate("You can't. It's midday.");
                    AddCharisma(-5);
                    break;
                case 4: ViewStats(); ViewInventory(); loops--; break;
            }
            loops++;
        }

        Narrate("It's becoming night.");
        Player.Say("Oh gee golly gosh it sure is getting dark!");
        Player.Say("I'm gonna have to choose what to do...");
        if (ItemCount("Diamonds") > 0)
        {
            choices = Build("Shout 'I have a diamond'", "Venture into the jungle", "Stay at island bay");
            route = Choose(choices) + 1;
        }
        else
        {
            choices = Build("Venture into the jungle", "Stay at island bay");
            route = Choose(choices) + 2;
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

        if (luck < 10)
        {
            int sticks = rand.Next(2, 6);
            Narrate($"You find §(6){sticks} sticks§(7).");
            AddToInventory("Sticks", sticks);
            AddCharisma(1);
        }
        else if (luck < 40)
        {
            int coconuts = rand.Next(1, 4);
            Narrate($"You found §(10){coconuts} coconut{(coconuts != 1 ? "s" : "")}§(7)!");
            AddToInventory("Coconuts", coconuts);
            AddCharisma(10);
        }
        else if (luck < 65)
        {
            if (stats[0] < 20)
            {
                Narrate("You find... §(4)used toilet paper§(7)?");
                AddToInventory("Used TP", 1);
                AddCharisma(-1);
            }
            else
            {
                Narrate("You only found more sand.");
                AddCharisma(-10);
            }
        }
        else if (luck < 85)
        {
            int gold = rand.Next(10, 50);
            Narrate($"You found §(14){gold} gold§(7)!");
            AddToInventory("Gold", gold);
            AddCharisma(20);
        }
        else
        {
            // 15% chance they get randomly sent on a random route:
            switch (rand.Next(0, 3))
            {
                case 0: Narrate("You found a §(9)diamond§(7)!"); Route1(); break;
                case 1: Route2(); break;
                case 2: Route3(); break;
            }
        }
    }
    static void Shout()
    {
        Person dave = new Person("Dave", ConsoleColor.DarkCyan, null);
        Person greg = new Person("Greg", ConsoleColor.Green, null);
        Player.Say("Help me!");
        Thread.Sleep(1000);
        int chance = rand.Next(1, 101);
        if (chance < 70)
        {
            Narrate("No-one responds.");
            AddCharisma(-10);
        }
        else if (chance < 99)
        {
            dave.Say("What in the world are you still doing here?");
            Player.Say("Dave, you came back!");
            dave.Say("And I'll leave as soon as I came.");
            Player.Say("Wait! Please help me out! I'm in dire need of your assistance!");
            dave.Say("Ugh okay, here's a §(9)diamond§(3).");
            AddToInventory("Diamonds", 1);
            Narrate("§(13)Maybe someone will be interested in this...");
            RestoreCharisma();
            Player.Say("See you later Dave!");
            Narrate("Dave disappears round a corner.");
            Player.Say("I probably should've followed him...");
            AddCharisma(-5);
        }
        else
        {
            greg.Say($"Ugh what is it now, {Player.Name}.");
            Player.Say("Please help me get off this island, Greg!");
            greg.Say("Ugh, fine. Get on the boat.");
            Narrate("Greg takes you back home.");
            Thread.Sleep(1000);
            throw new WonGameException(0);
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
        CachedSound sandDestroy = new CachedSound(@"Sounds\DigSand.wav");

        MainConsole.Clear();

        // [0] sand | [1] empty | [2] gold coin | [3] diamond | [4] stick 
        int[,] map = new int[5, 5];
        bool diamond = false;
        int xChoice = 0, yChoice = 0;
        Print($"You have 5 choices remaining. ", 2, 0, 0);
        DrawDigGame(map, xChoice, yChoice);
        for (int choices = 0; choices < 5; choices++)
        {
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
            Print($"You have {4 - choices} choice{(choices == 4 ? "" : "s")} remaining. ", 2, 0, 0);

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
                charisma = 1;
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

            DrawDigGame(map, xChoice, yChoice);
            Print($"You found {count} {item}§(15)!", 1, 0, 8 + choices * 2);
            AddCharisma(charisma);
        }

        Print($"You have 0 choices remaining.", 2, 0, 0);
        DrawDigGame(map, -1, -1);
        if (diamond) { Narrate("§(13)Maybe someone will be interested in this diamond...", y:18); }
        Narrate("Press any key to continue.", 0, 0, 20);
        ClearKeyBuffer();
        Console.ReadKey();
        AudioPlaybackEngine.Instance.PlaySound(menuBleep);
        MainConsole.Clear();
    }


    /// CHAPTER 2

    /// Diamond route : Go home route
    static void Route1()
    {
        Person chanelle = new Person("Chanelle", ConsoleColor.DarkMagenta, null);
        Person addison = new Person("Addison", ConsoleColor.DarkBlue, null);
        Person shaniece = new Person("Shaniece", ConsoleColor.DarkGreen, null);
        Person mackenzie = new Person("Mackenzie", ConsoleColor.DarkRed, null);

        Player.Say("I have §(9)diamonds§(11)!");
        Thread.Sleep(1000);
        Narrate("The paparazzi appear, asking for your diamonds.");
        chanelle.Say("OMGOODNESS you have §(9)DIAMONDS§(5)!?");
        addison.Say($"WOW, {Player.Name.ToUpper()}, I LOVE YOU SO MUCH!");
        shaniece.Say($"You don't mind sharing do you?");
        mackenzie.Say($"Follow us, we'll give you whatever you want for your diamonds!");
        Narrate("The ladies take you to their village.");
        Thread.Sleep(1000);
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Fuzzball Parade.mp3");
        ClearKeyBuffer();
        MainConsole.Clear();

        DigArt(3);
        chanelle.Say($"Welcome to our village, {Player.Name}!");
        addison.Say("Have a look around!");
        shaniece.Say("Take a look at my hotel for a place to sleep at night.");
        mackenzie.Say("Check out my shop to spend that §(9)diamond §(4)of yours.");

        Player.Say("Hmm, so I can either start out a new life here, or I can get a boat and try to go home.");
        if (Choose(Build("Go home", "Live here")) == 1)
        {
            LiveInVillage(); // otherwise stay on this route
        }

        ConsoleMessage[] options = Build("Mackenzie's Shop", "Shaniece's Hotel", "View Stats", "Search Further East");
        bool skipRamble = false; // cleans the transition to the east side
        bool finished = false;
        while (!finished)
        {
            DigArt(3);
            if (!skipRamble) { Player.Say("What do I do now?"); }
            skipRamble = false;
            switch (Choose(options))
            {
                case -1: MainMenu(); break;
                case 0: Shop(false); break;
                case 1: Hotel(); break;
                case 2: ViewStats(); ViewInventory(); break;
                case 3:
                    if (options[3].Contents == "Search Further East")
                    {
                        if (EastSide()) // east side returns true once player has spoken with scammer with sufficient materials
                        {
                            options[3] = new ConsoleMessage("Set sail", ConsoleColor.Magenta);
                        }
                        skipRamble = true;
                    }
                    else // If they have sufficient materials and have spoken with scammer
                    {
                        finished = true;
                    }
                    break;
            }
            if (!skipRamble) { MainConsole.Clear(); }
        }

        Narrate("You wander towards the bay to find a shambolic raft.");
        Player.Say("Well I've not really got a choice, have I?");
        Narrate("You board the boat and set sail.");
        Thread.Sleep(1000);

        MainConsole.Clear();

        Narrate("You have one more choice to make.");
        Player.Say("Now how should I use this boat and map?");
        switch(Choose(Build("Search for treasure", "Sail home"), false))
        {
            case 0: SailBoatHome(); break;
            case 1: SearchForTreasure(); break;
        }
    }
    static bool EastSide()
    {
        bool canLeaveIsland = false;
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
                case 1: canLeaveIsland = ScammerHouse(); choices[1] = "Chanelle's House"; break;
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
                    else if (rand.NextDouble() > 0.1) // 10% chance they dont get gold. (because they need to get on with the game)
                    {
                        Narrate("There is a couple gold coins on the floor.");
                        Narrate("Collect the gold coins?");
                        switch (Choose(Build("Yes, collect the gold", "No, leave it")))
                        {
                            case 0: AddToInventory("Gold", rand.Next(5, 18)); Narrate("You collect the gold coins."); break;
                            case 1: Narrate("You ignore the gold coins."); break;
                        }
                    }
                    Thread.Sleep(1000);
                    break;

                case 3: return canLeaveIsland;
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
            if ((ItemCount("Sticks") > 0 && ItemCount("Rope") > 0) || ItemCount("Roblox gift card") > 0 || ItemCount("Used TP") > 0)
            {
                if (ItemCount("Old map") > 0)
                {
                    chanelle.Say("Who is it?");
                    Player.Say($"It's {Player.Name} again.");
                    chanelle.Say("Ugh how badly do you want this boat?");
                    Player.Say("I already payed for it!");
                    chanelle.Say("Fine, fine, don't get your knickers in a twist.");
                    chanelle.Say("I've docked it at the island bay. Take it! I don't want it.");
                    Narrate("Chanelle shuts the door on you.");
                    return true;
                }
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
                    chanelle.Say("Aw that's a real shame. If you ever change your mind just come back!");
                    Narrate("You leave the house.");
                    break;
            }

            Thread.Sleep(1000);
        }
        return false;
    }

    static Tuple<ConsoleMessage, int>[][] shop = BuildShelves(NewShelf(NewItem("Bunch o' bananas", 5), NewItem("Teddy bear", 10), NewItem("Shovel", 20)),
                                                             NewShelf(NewItem("Roblox gift card", 100), NewItem("Paper", 3), NewItem("Rope", 5)),
                                                             NewShelf(NewItem("Diamonds", 999), NewItem("Pencil", 3), NewItem("Sticks", 1)));
    static void Shop(bool villageRoute)
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Local Forecast.mp3");

        Person mackenzie = new Person("Mackenzie", ConsoleColor.DarkRed, null);

        mackenzie.Say($"Hey, {Player.Name}, welcome to my shop!");
        if (!villageRoute)
        {
            if (!knowledge.Contains("Spoken with shopkeeper"))
            {
                Player.Say($"I have §(14){inventory["Gold"]} gold§(11).");
                mackenzie.Say("And what about that §(9)diamond§(4)?");
                if (knowledge.Contains("Scammed"))
                {
                    Player.Say("Chanelle took it from me!");
                    Player.Say("She said she'd give me a boat in return and then kicked me out!");
                    mackenzie.Say("Oh you can't trust that Chanelle.");
                    mackenzie.Say("If you're looking to get off this island, you'll be needing §(13)supplies §(4)from me!");
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
        }

        (int shelfI, int itemI) = ShopMenu(shop);
        string item = shop[shelfI][itemI].Item1.Contents;
        int cost = shop[shelfI][itemI].Item2;

        if (shelfI != -1 && shop[shelfI][itemI].Item1.Contents != "---")
        {
            if (ItemCount("Gold") < cost)
            {
                mackenzie.Say($"Sorry, you only have §(14){ItemCount("Gold")} gold§(4).");
                mackenzie.Say($"The {item} costs §(14){cost} gold§(4).");
                // earn money?
            }
            else
            {
                mackenzie.Say($"Are you sure you would like to buy {item} for §(14){cost} gold§(4)?");
                switch (Choose(Build($"Yes, buy the {item}", "No, I don't want it")))
                {
                    case 0:
                        Buy(cost);
                        AddToInventory(item, 1);
                        shop[shelfI][itemI] = NewItem("---", 0);
                        break;
                }
            }
        }

        Narrate("§(8)Press any key to exit the shop.");
        ClearKeyBuffer();
        Console.ReadKey();

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
                    }
                    break;
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
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Porch Swing Days.mp3");

        Person shaniece = new Person("Shaniece", ConsoleColor.DarkGreen, null);

        if (ItemCount("Hotel Pass") == 0)
        {
            shaniece.Say("Welcome to my hotel.");
            shaniece.Say("Would you like to book a room?");
            shaniece.Say("It's §(14)25 gold §(2)for one night.");
            Narrate("Book the night for 25 gold?");
            switch (Choose(Build("Yes, book the night", "No, not tonight")))
            {
                case 0:
                    int money = Buy(25);
                    if (money > 0) { Player.Say($"Oh, nevermind. I only have §(14){money} gold§(11)."); }
                    else { shaniece.Say("Thank you. Come in whenever you like."); AddToInventory("Hotel Pass", 1); }
                    break;
                case 1: shaniece.Say("Come again soon!"); break;
            }
        }
        else
        {
            shaniece.Say($"Would you like to check in for the night, {Player.Name}?");
            switch (Choose(Build("Yes, stay the night", "No, come back later")))
            {
                case 0:
                    Narrate("You go to bed.");
                    Narrate("Getting a good nights sleep helps you feel better.");
                    RestoreCharisma();
                    knowledge.Add("Been to bed");
                    break;
            }
        }

        Thread.Sleep(1000);
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Fuzzball Parade.mp3");
    }
    static void SailBoatHome()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Salty Ditty.mp3");

        Player.Say("So the map seems to say I go §(13)right§(11), then §(13)left§(11), another §(13)left§(11), then §(13)forward§(11).");
        Player.Say("Alright! I'm facing North right now, so...");
        bool failed = false;

        ConsoleMessage[] choices = Build("Backward", "Right", "Forward", "Left");
        int choice = Choose(choices);
        if (choice != 1) { failed = true; }
        Player.Say($"So I guess I'll go {choices[choice].Contents} first.");

        choices = Build("Onward", "Up", "North", "Staight");
        choice = Choose(choices);
        if (choice != 2) { failed = true; }
        Player.Say($"Then I'll go {choices[choice].Contents}.");

        Player.Say("Might help to know I'm right handed.");
        choices = Build("Perpendicular weak hand", "Reverse", "Sideways strong hand", "Above");
        choice = Choose(choices);
        if (choice != 0) { failed = true; }
        Player.Say($"Now I'll go in a {choices[choice].Contents} sort of direction.");

        choices = Build("Dexter", "Pursue", "Sinister", "Recede");
        choice = Choose(choices);
        if (choice != 1) { failed = true; }
        Player.Say($"And finally, I choose to {choices[choice].Contents}!");


        if (failed)
        {
            knowledge.Add("Failed map");
            Player.Say("Well it doesn't really look like I've gone the right way...");
            Narrate("All of a sudden, you hear a perculiar sound...");
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\CartoonBoing.wav"), true);
            Thread.Sleep(500);
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Thinking Music.mp3");
            Player.Say("What in the world could that be?");
            Narrate("You feel yourself descending.");
            Player.Say("Hmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm...");
            Player.Say("OH WAIT I'M SINKING!", sleep: 30);
            Thread.Sleep(350);
            AudioPlaybackEngine.Instance.StopLoopingMusic();
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\GameOver.wav"), true);
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Oppressive Gloom.mp3");
            CenterText(Build("Game Over"));

            throw new NewGameException();

        }
        
        Player.Say("Huzzah! I have bested the map!");
        Player.Say("There it is—Great Britian.");
        Player.Say("They sure were right in calling it great.");
        Narrate("You found your way home.");
        Narrate("The end...", sleep:300);
        Thread.Sleep(1000);
        throw new WonGameException(1);
    }
    static void SearchForTreasure()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Salty Ditty.mp3");

        Player.Say("So the map seems to say I go this way!");
        Player.Say("Treasure of the seas, here I come!");
        Narrate("All of a sudden, you hear a perculiar sound...");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\CartoonBoing.wav"), true);
        Thread.Sleep(500);
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Thinking Music.mp3");
        Player.Say("What in the world could that be?");
        Narrate("You feel yourself descending.");
        Player.Say("Hmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm...");
        Player.Say("OH WAIT I'M SINKING!", sleep: 30);

        SinkingShip();
        OtherIsland();
    }
    static void SinkingShip()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Mistake the Getaway.mp3");
        MainConsole.Clear();
        Print($"§(11){Player.Name}: OH WAIT I'M SINKING!");
        Narrate("Choose an item to fix the leakage.");

        // to beat this minigame inventory must contain: sticks and rope || used TP || Roblox gift card

        ConsoleMessage[] items = ConvertStringArray(inventory.Keys.ToArray());

        bool sticksApplied = false;
        bool ropeApplied = false;
        while (!(sticksApplied && ropeApplied))
        {
            int itemChosen = Choose(items.Take(4).ToArray());
            switch (items[itemChosen].Contents)
            {
                case "Gold":
                    Narrate("You... throw the gold at the leak?");
                    break;
                case "Coconuts":
                    Narrate("You lob a coconut at the leak. It's now leaking at twice the speed.");
                    break;
                case "Bunch o' bananas":
                    Narrate("You lob a banana at the leak. It's now leaking at one and a half times the speed.");
                    break;
                case "Diamonds":
                    Narrate("You throw a priceless diamond at the leak. It does nothing.");
                    AddCharisma(-20);
                    break;
                case "Teddy bear":
                    Narrate("You throw a teddy bear at the leak. It does nothing.");
                    Narrate("A child could've had that.");
                    AddCharisma(-5);
                    break;
                case "Paper":
                    Narrate("You place a piece of paper of the leak.");
                    Narrate("The paper gets wet.");
                    break;
                case "Pencil":
                    Narrate("You pierce another leak in the boat with a pencil.");
                    Narrate("The ship is now sinking at one and three quarters the speed.");
                    break;
                case "Shovel":
                    Narrate("You lob a shovel at the leak.");
                    Narrate("The ship is now sinking at quadruple the speed.");
                    break;
                case "Roblox gift card":
                    Narrate("You administer the Roblox gift card to the leak, tears in your eyes.");
                    Player.Say("I payed for those Robux.");
                    Narrate("Miraculously, the Roblox gift card fixes the leak!");
                    sticksApplied = true; ropeApplied = true;
                    break;
                case "Used TP":
                    Narrate("You carefully apply the used toilet paper to the leak.");
                    Narrate("It works incredibly well.");
                    sticksApplied = true; ropeApplied = true;
                    break;
                case "Sticks":
                    sticksApplied = true;
                    if (!ropeApplied) { Narrate("The sticks help, they could use some §(5)rope§(7)."); }
                    else { Narrate("The sticks seal the leak."); }
                    break;
                case "Rope":
                    ropeApplied = true;
                    if (!sticksApplied) { Narrate("The rope helps, it could use some §(5)sticks§(7)."); }
                    else { Narrate("The sticks seal the leak."); }
                    break;
            }

            // remove their choice from their inventory
            items = items.Where((source, index) => index != itemChosen).ToArray();
        }
        Thread.Sleep(1000);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
    }
    static void OtherIsland()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Beach Sounds.wav");
        Player.Say("Yay, I'm nearly at the island!");
        Narrate("The boat inches closer to the island. As soon as it makes contact it collapses.");
        AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\WoodCrumble.wav"), true);
        //Player.Voice = new CachedSound(@"Sounds\VoiceYou.wav");
        Player.Say("...", sleep: 150);
        //Player.Voice = null;
        Thread.Sleep(1000);
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Cottages.wav");
        Player.Say("Hmmm, what do I do now?");
        Narrate("You look at the map very closely.");
        Thread.Sleep(1000);
        Player.Say("Ah! An X marks the spot!");

        if (ItemCount("Shovel") < 0 && ItemCount("Sticks") < 0)
        {
            Player.Say("But I have nothing to dig it up with...");
            Player.Say("I guess I'll have to forage for sticks.");
            while (!inventory.ContainsKey("Sticks"))
            {
                Choose(Build("Forage for sticks"));
                Forage();
            }
            Player.Say("Finally, I can use these sticks to uncover what lays under the X!");
        }
        else
        {
            Player.Say($"Thank goodness I still have {(ItemCount("Shovel") > 0 ? "this shovel" : "these sticks")}!");
        }

        Narrate("You guide yourself to the X.");
        Print("§(8)Press [SPACE] continuously to dig up the sand.");
        for (int i = 0; i < 4; i++)
        {
            int spaceCount = 0;
            int spacesToBreakSand = 5;
            while (spaceCount < spacesToBreakSand)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Spacebar) { spaceCount++; }
                spacesToBreakSand = rand.Next(4, 8);
            }
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\DigSand.wav"));
        }

        Person frank = new Person("Frank The Car Dealer", ConsoleColor.DarkBlue, null);
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\Discovery.wav"), true);
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Arcadia.mp3");
        Player.Say("OH MY GOODNESS GRACIOUS ME!");
        Player.Say("IT'S HEAVENLY!");
        Player.Say("IT'S GLORY EXCEEDES THAT OF MY TOASTER!");
        Player.Say("IT'S...");
        Player.Say("IT'S....");
        AudioPlaybackEngine.Instance.StopLoopingMusic();
        frank.Say("We've been trying to reach you concerning car's extended warranty.");
        Player.Say("Ugh thanks for ruining my moment, Frank.");
        frank.Say("Sorry, but I have a message regarding automotive extended warranties.");
        //frank.Voice = new CachedSound(@"Sounds\VoiceDave.wav");
        //Player.Voice = new CachedSound(@"Sounds\VoiceYou.wav");
        Player.Say("...", sleep: 200);
        frank.Say("...", sleep: 200);
        //frank.Voice = null;
        //Player.Voice = null;
        AudioPlaybackEngine.Instance.ResumeLoopingMusic();
        Player.Say("IT'S A SPOON!");
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Feelin Good.mp3");
        Narrate("You love spoons.");
        Player.Say("YIPEE!");
        Player.Say("Oh by the way Frank if you take me back home then I'll talk about extending my warranty.");
        frank.Say("Oooh! Goodie goodie!");
        Narrate("You and Frank travelled home, and you never had to look back at that island again.");
        Narrate("You bought an extended warranty for your car, Frank was ecstatic.");
        Narrate("Greg and Dave went on to push more people off boats, turns out they were hired to do it as the island needed more tourists.");

        Narrate("The end.", sleep: 300);
        Thread.Sleep(1000);
        throw new WonGameException(1);
    }

    /// Live in town route
    static void LiveInVillage()
    {
        AddToInventory("Gold", 50);
        Narrate("The council kindly give you §(14)50 gold §(7)to help you begin your endeavours in the village.");
        int initGold = ItemCount("Gold");
        ConsoleMessage[] options = Build("Mackenzie's Shop", "Shaniece's Hotel", "View Chores", "View stats");
        List<string> chores = new() { "Buy groceries", "Go to bed", "Restore charisma", "Pay taxes" };
        bool skipRamble = false; // cleans the transition to the east side
        bool finished = false;
        while (!finished)
        {
            DigArt(3);
            if (!skipRamble) { Player.Say("What do I do now?"); }
            skipRamble = false;
            switch (Choose(options))
            {
                case -1: MainMenu(); break;
                case 0: Shop(true); if (ItemCount("Bunch o' bananas") > 0) { chores.Remove("Buy groceries"); } break;
                case 1: Hotel(); if (knowledge.Contains("Been to bed")) { chores.Remove("Go to bed"); } break;
                case 2: ViewChores(chores); break;
                case 3: ViewStats(); ViewInventory(); break;
            }
            if (!skipRamble) { MainConsole.Clear(); }
            if (stats[1] == 100) { chores.Remove("Restore charisma"); }
            if (chores.Count == 1) { finished = true; }
        }

        Player.Say("Finally, I've done everything I need to do to become a normal citizen here!");
        Player.Say("Now I need to pay my taxes.");
        if (Choose(Build("Pay taxes", "Don't pay taxes")) == 1) { TaxEvasion(); } // will run into a win game or loose game so no else needed
        // pay taxes route
        MainConsole.Clear();
        int currentGold = ItemCount("Gold");
        int goldSpent = initGold - currentGold;

        Player.Say($"So I started with {goldSpent} gold.");
        Player.Say($"And now I have {currentGold} gold.");
        Player.Say("How much gold have I spent? ");
        int guess = ReadInt();
        if (guess != goldSpent) // ok I KNOW this is incredibly repetetive code but leave me be I have 1000 more routes to add
        {
            Player.Say($"It's, err- I think it's about {guess}?");
            Thread.Sleep(500);
            CenterText(Build("Days pass..."), time: 2000, audioLocation: @"Sounds\DaysPass.wav"); MainConsole.Clear();
            Narrate($"It was not {guess}. The council were not impressed with your inability to subtract.");
            Thread.Sleep(500);
            AudioPlaybackEngine.Instance.StopLoopingMusic();
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\GameOver.wav"), true);
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Oppressive Gloom.mp3");
            CenterText(Build("Game Over"));

            throw new NewGameException();    
        }

        Player.Say("Now I need 17.5% of that (I'll round it down to the nearest integer no-one'll know)");
        guess = ReadInt();
        int tax = (int)(goldSpent * 175 / 1000);

        if (guess != tax)
        {
            Player.Say($"It's, err- I think it's about {guess}?");
            Thread.Sleep(500);
            CenterText(Build("Days pass..."), time: 2000, audioLocation: @"Sounds\DaysPass.wav"); MainConsole.Clear();
            Narrate($"It was not {guess}. The council were not impressed with your inability to multiply percentages.");
            Thread.Sleep(500);
            AudioPlaybackEngine.Instance.StopLoopingMusic();
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\GameOver.wav"), true);
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Oppressive Gloom.mp3");
            CenterText(Build("Game Over"));

            throw new NewGameException();
        }

        Player.Say("Now whats my current balance minus the tax?");
        guess = ReadInt();

        if (guess != currentGold - tax)
        {
            Player.Say($"It's, err- I think it's about {guess}?");
            Thread.Sleep(500);
            CenterText(Build("Days pass..."), time: 2000, audioLocation: @"Sounds\DaysPass.wav"); MainConsole.Clear();
            Narrate($"It was not {guess}. The council were not impressed with your inability to subtract.");
            Thread.Sleep(500);
            AudioPlaybackEngine.Instance.StopLoopingMusic();
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\GameOver.wav"), true);
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Oppressive Gloom.mp3");
            CenterText(Build("Game Over"));

            throw new NewGameException();
        }

        Player.Say("Huzzah! I have deduced my tax. Yipee!");
        Thread.Sleep(500);
        throw new WonGameException(1);
    }
    static void ViewChores(List<string> chores)
    {
        MainConsole.Clear();
        Print("Current chores", 2);
        for(int i = 0; i < chores.Count; i++)
        {
            Print($"{i + 1}. {chores[i]}");
        }
        Print("");
        Print("§(8)Press any key to continue.");
        Console.ReadKey();
    }
    static void TaxEvasion()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Adventures Of Captain Trillian.mp3");

        Player.Say("AHHH!");
        Narrate("You begin running. Watch out for giant rocks to jump over.");
        Thread.Sleep(3000);
        QuickJump(1000);
        Player.Say("Phew, I made i-");
        QuickJump(600);
        Player.Say("Golly-gee gosh great goodness! That sure was close.");
        Thread.Sleep(2420);
        QuickJump(400); // 400 ms F's in the chat if they dont get it
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Boogie Party.mp3");
        ClearKeyBuffer();
        Narrate("You did it! In the nick of time aswell!");
        Narrate("You continued to run for the rest of all eternity.");
        Narrate("You were never caught. Not ever.");
        Narrate("The end.", sleep: 300);
        knowledge.Add("Evaded taxes");
        throw new WonGameException(1);
    }
    static void QuickJump(int time)
    {
        ClearKeyBuffer();
        Print("§(12)GIANT ROCK! JUMP WITH [SPACE]!");
        Thread.Sleep(time);
        if (!Console.KeyAvailable)
        {
            ClearKeyBuffer();
            MainConsole.Clear();
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\Crash.mp3"), true);
            AudioPlaybackEngine.Instance.ResumeLoopingMusic();
            ClearKeyBuffer();
            CenterText(Build("You died..."));
            throw new NewGameException();
        }
    }



    /// Monkey route
    static void Route2()
    {
        Narrate("As you wander, you see something coming over the horizon...");
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Jaunty Gumption.mp3");

        Person craig = new Person("Craig The Monkey", ConsoleColor.DarkGreen, null);
        Person walt = new Person("Walt The Monkey", ConsoleColor.DarkBlue, null);

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
                        Player.Say("Wait-wha");
                        AudioPlaybackEngine.Instance.StopLoopingMusic();
                        MainConsole.Clear();
                        MonkeyBattle();
                    }
                }
            }
        }

        MonkeyRace();
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Winner Winner.mp3");

        craig.Say("Ooo-oo Aa-aa! (The human beat us!)");
        walt.Say("Aaa-aa Eee-ee! (The human must be our new king!)");
        craig.Say("Eee-ee Aa-aa! (We shall take the new king home!)");
        walt.Say("Aaa-aa Ooo-oo! (The human will battle for the throne!)");

        Player.Say("Wait-wait-wait-wait.");

        if (Choose(Build("Battle for the monkey throne?", "No... maybe later.")) == 0)
        {
            AudioPlaybackEngine.Instance.StopLoopingMusic();
            MainConsole.Clear();
            MonkeyBattle();
        }

        craig.Say("Eee-eee Oo-ooo! (Fine, human will play with us!)");
        MonkeyRace();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Winner Winner.mp3");
        walt.Say("Ooo-oo Aaa-aaa! (Thank you for playing with us!)");
        craig.Say("Aaa-aa Ee-eee! (We will go home now!)");
        Narrate("The end.", sleep: 500);
        Thread.Sleep(1000);
        throw new WonGameException(2);
    }
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
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Adventures Of Captain Trillian.mp3");
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
        Print("§(10)FINISH!");

        if (monkeyPos1 == 0 || monkeyPos2 == 0)
        {
            AudioPlaybackEngine.Instance.StopLoopingMusic();
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\GameOver.wav"));
            Narrate("You lost...");
            Narrate("The monkeys ransack all your belongings.");
            AddCharisma(-50);
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Oppressive Gloom.mp3");
            CenterText(Build("Game Over"));
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
        king.Say("You will battle me, and then we shall see who has the stronger wit!", 2);
        
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
            AudioPlaybackEngine.Instance.PlaySound(new CachedSound(@"Sounds\GameOver.wav"), true);
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Oppressive Gloom.mp3");
            CenterText(Build("Game Over"));
            throw new NewGameException();
        }
        else
        {
            king.Say("Ugh, cheater! Nobody beats me!", 2);
            king.Say("Guards, take this imbecile to the volcano!", 2);
            Player.Say("YOU WHA");
            MainConsole.Clear();
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
        
        if (Choose(Build("Escape volcano", "Stay in volcano")) == 0)
        {
            MonkeyChase();
        }

        Narrate("The monkeys, amazed by your skill, save you and promote you to their new leader.");
        Narrate("They overthrow the old king, who now eats bananas in a cave.");
        Narrate("You, however, went on to lead the monkeys to an unstoppable empire.");
        Narrate("As for all the other monkeys, they loved their new king.");
        Narrate("The end.", sleep: 300);
        Thread.Sleep(1500);
        throw new WonGameException(2);
    }
    static void MonkeyChase()
    {
        MainConsole.Clear();
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Adventures Of Captain Trillian.mp3");

        Player.Say("AHHH!");
        Narrate("You begin running from the hoard of monkeys. Watch out for giant rocks to jump over.");
        Thread.Sleep(3000);
        QuickJump(1000);
        Player.Say("Phew, I made i-");
        QuickJump(600);
        Player.Say("Golly-gee gosh great goodness! That sure was close.");
        Thread.Sleep(2420);
        QuickJump(400); // 400 ms F's in the chat if they dont get it
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Boogie Party.mp3");
        ClearKeyBuffer();
        Narrate("You did it! In the nick of time aswell!");
        Narrate("You continued to run for the rest of all eternity.");
        Narrate("You were never caught. Not ever.");
        Narrate("The end.", sleep: 300);
        throw new WonGameException(2);
    }

    /// Pirate Route
    static void Route3Prologue()
    {
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Modern Jazz Samba.mp3");

        Person captain = new Person("Captain Blackbeard", ConsoleColor.DarkRed, null);
        Person carl = new Person("Stinkin' Carl", ConsoleColor.DarkGreen, null);

        Narrate("A pirate ship approaches!");
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
        MainConsole.Clear();
        Narrate("You see something coming over the horizon...");
        Route3Prologue();
        Person captain = new Person("Captain Blackbeard", ConsoleColor.DarkRed, null);
        Person carl = new Person("Stinkin' Carl", ConsoleColor.DarkGreen, null);

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Captain Scurvy.mp3");
        captain.Say("SCRUB THE DECK!");

        if (Choose(Build("Scrub the deck", "No, fight the pirates")) == 1)
        {
            PirateBattle();
        }

        ScrubTheDeck();
        Console.SetCursorPosition(0, 1);
        captain.Say("Shiver me timbers!");
        Thread.Sleep(500);
        captain.Say("IS WHAT I WOULD SAY IF YE MATEYS WEREN'T SO SLOW!", sleep: 30);
        Thread.Sleep(1000);

        captain.Say("NOW CLIMB THE RIGGING!");
        Narrate("What the captain said about your speed really hurt your feelings.");
        Narrate("Are you going to allow his bullying to continue?");

        if (Choose(Build("Climb the rigging", "No, fight the pirates")) == 1)
        {
            PirateBattle();
        }

        ClimbTheRigging();

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Modern Jazz Samba.mp3");
        Player.Name = "Cutlass " + Player.Name;

        captain.Say("Arggg, ye beat me fellow pirateers!");
        Player.Say("Blimey! Me lily-livered, bilge-sucking hearties are soon-to-be shark bait!");
        captain.Say($"Ye truly are the stronger pirate, {Player.Name}.");
        Thread.Sleep(1000);
        throw new WonGameException(3);
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

        CachedSound wipeDeck = new CachedSound(@"Sounds\DeckWipe.mp3");
        CachedSound ding = new CachedSound(@"Sounds\DeckSparkle.mp3");
        for (int i = 5; i >= 0; i--)
        {
            MainConsole.Refresh(); // deletes everything, then rewrites only the logged messages.
            DrawDeck(deck);
            int spaceCount = 0;
            while (spaceCount < 5)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Spacebar)
                {
                    // dont do wipe sound every time or it sounds bad
                    if (spaceCount % 2 == 0) { AudioPlaybackEngine.Instance.PlaySound(wipeDeck); }
                    spaceCount++;
                }
            }

            for (int j = 0; j < 9; j++)
            {
                deck[i, j] = 0;
            }
            AudioPlaybackEngine.Instance.PlaySound(ding);
        }

        DrawDeck(deck);

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

        Print("§(8)Press [SPACE] as fast as you can to climb faster that the other pirates.", 1, 0, Console.WindowHeight - 3);
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
        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Adventures Of Captain Trillian.mp3");
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
            Thread.Sleep(500);
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
    static readonly ConsoleMessage[] goodInsults = Build("You fight like a dairy farmer!", "I've spoken with apes more polite than you!", "I once owned a dog that was smarter than you.",
                                                         "You're not invited to my birthday party!", "You're no match for my brains you poor fool.", "You have the manners of a beggar.");
    static readonly ConsoleMessage[] badInsults = Build("You're as blind as a bat!", "You're as ugly as an elephant.", "Everything you say is stupid!", "You probably can't even go cross-eyed!",
                                                        "You coward!", "You make me sick!", "I'd like my steak chicken-fried.", "I bet your wardrobe isn't even color-coordinated!");
    static Person carl = new Person("Stinkin' Carl", ConsoleColor.DarkGreen, null);
    static void PirateBattle()
    {
        Person captain = new Person("Captain Blackbeard", ConsoleColor.DarkRed, null);

        captain.Say("Blimey! Me lily-livered, bilge-sucking hearty is soon-to-be shark bait!");
        captain.Say($"Avast ye, crew! Who reckons he can plunder this scallywag's booty!");
        Player.Say("Oh dear.");
        carl.Say("I'll fight 'em Cap'n!");
        captain.Say($"Well so it be! {carl.Name} shall battle {Player.Name}!");
        Player.Say("Oh no");

        AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Bushwick Tarantella.mp3");
        MainConsole.Clear();


        DigArt(6, 0, 4);
        carl.Say("This is the end for you, you gutter-crawling cur!");
        DrawHealth();
        weapons = ConvertStringArray(inventory.Keys.ToArray());
        while (carlHealth > 1 && playerHealth > 1 && !surrendered)
        {
            if (playerDistracted)
            {
                Narrate("You are too distracted to make a move!", sleep: 55);
                playerDistracted = false;
            }
            else
            {
                PlayerAttack();
                weapons = ConvertStringArray(inventory.Keys.ToArray()); // update weapons as they may have used up an item
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
            WalkThePlank();
        }
        else if (playerHealth < 1)
        {
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Man down.mp3");
            Player.Say("By golly gosh you've made me toast!");
            carl.Say("I always knew you weren't a real pirate!");
            Thread.Sleep(1000);
            WalkThePlank();
        }
        else
        {
            AudioPlaybackEngine.Instance.PlayLoopingMusic(@"Music\Vivacity.mp3");
            carl.Say("Aye, I've been bested. Me booty be the Captains.");
            Narrate("Carl yields.");
            Thread.Sleep(1000);
            MainConsole.Clear();
            captain.Say($"Ay, {Player.Name} ye truly were the stronger pirate.");
            Player.Say("ARG! I KNEW IT FROM THE START!");
            Narrate("You begin an unstoppable pirating career, collecting booty and tracking down treasure.");
            Narrate("Soon Captain Blackbeard retires and now resides in a care home.");
            Narrate("You, however, continued your pirating to your very last breath.");
            Narrate("The end.", sleep: 300);
            Thread.Sleep(2000);
            throw new WonGameException(3);
        }
    }
    static void PlayerAttack()
    {
        weapons ??= new ConsoleMessage[1]; // weapons should never be null at this point. This is for the sake of stopping it from crying about null dereferences.
        bool madeMove = false;
        int choice;
        while (!madeMove)
        {
            madeMove = true;
            int methodOfAttack = Choose(Build("Attack", "Use item", "Insult", "Surrender"), drawHealth: true);
            switch (methodOfAttack)
            {
                case -1: MainMenu(); madeMove = false; break;
                case 0:
                    if (weapons.Length == 0) { Narrate("You have no items left to attack with! Try an insult."); break; }
                    choice = Choose(weapons, drawHealth: true);
                    if (choice == -1) { madeMove = false; break; } // escape

                    switch (weapons[choice].Contents) // attack
                    {
                        case "Gold":
                            Narrate("You hurl a handful of gold coins at Carl.", sleep: 55);
                            carl.Say("Ouch!");
                            AddToHealth("carl", -15);
                            AddToInventory("Gold", -5);
                            break;
                        case "Sticks":
                            Narrate("You toss the stick at Carl.", sleep: 55);
                            carl.Say("Nice throw.");
                            AddToHealth("carl", -8);
                            AddToInventory("Sticks", -1);
                            break;
                        case "Coconuts":
                            Narrate("You lob a coconut at Carl.", sleep: 55);
                            carl.Say("ARGH!");
                            Narrate("§(12)CRITICAL HIT!", sleep: 55);
                            AddToHealth("carl", -30);
                            AddToInventory("Coconuts", -1);
                            break;
                        case "Used TP":
                            AudioPlaybackEngine.Instance.PauseLoopingMusic();
                            Narrate("You apply the used toilet paper to Carl."); carl.Voice = new CachedSound(@"Sounds\VoiceDave.wav");
                            carl.Say("...", sleep: 150); carl.Voice = null;
                            Narrate("§(12)CRITICAL HIT!", sleep: 30);
                            AddToHealth("carl", -99);
                            AddToInventory("Used TP", -1);
                            AudioPlaybackEngine.Instance.ResumeLoopingMusic(); break;
                        case "Diamonds":
                            Narrate("You propel a priceless diamond at Carl.", sleep: 55);
                            carl.Say("Thanks!");
                            AddCharisma(-15);
                            AddToHealth("carl", 15);
                            AddToInventory("Diamonds", -1);
                            break;

                    }
                    break;

                case 1:
                    if (weapons.Length == 0) { Narrate("You have no items left to use! Try an insult."); break; }
                    choice = Choose(weapons, drawHealth: true);
                    if (choice == -1) { madeMove = false; break; } // escape

                    switch (weapons[choice].Contents) // use item
                    {
                        case "Gold":
                            Narrate("You... Eat the gold?", sleep: 55);
                            Player.Say("Ouch!");
                            AddToHealth("player", -10);
                            AddToInventory("Gold", -5);
                            break;
                        case "Sticks":
                            Narrate("You arrange the sticks into a pretty pattern.", sleep: 55);
                            Narrate("§(10)Carl is distracted for 1 round!", sleep: 55);
                            carlDistracted = true;
                            AddToInventory("Sticks", -1);
                            break;
                        case "Coconuts":
                            Narrate("You eat the coconut.", sleep: 55);
                            Player.Say("Mmm! Delicious!");
                            AddToHealth("player", 30);
                            AddToInventory("Coconuts", -1);
                            break;
                        case "Used TP":
                            AudioPlaybackEngine.Instance.PauseLoopingMusic();
                            Narrate("You... use the toilet paper."); carl.Voice = new CachedSound(@"Sounds\VoiceDave.wav");
                            carl.Say("...", sleep: 150); carl.Voice = null;
                            Narrate("Carl is disgusted.");
                            Narrate("§(10)Carl is distracted for 1 round!", sleep: 55);
                            AddToInventory("Used TP", -1);
                            carlDistracted = true;
                            AudioPlaybackEngine.Instance.ResumeLoopingMusic(); break;
                        case "Diamonds":
                            Narrate("You stare at the diamond, amazed by it's reflectivity.", sleep: 55);
                            Narrate("§(12)You are distracted for 1 round!", sleep: 55);
                            AddToInventory("Diamonds", -1);
                            playerDistracted = true; break;
                    }
                    break;

                case 2:
                    ConsoleMessage[] insults = Build(goodInsults[rand.Next(0, goodInsults.Length)], badInsults[rand.Next(0, badInsults.Length)], badInsults[rand.Next(0, badInsults.Length)]);
                    insults = insults.OrderBy(x => rand.Next()).ToArray(); // randomise order so correct isnt always first
                    choice = SimpleChoose(insults, drawHealth: true);
                    if (choice == -1) { madeMove = false; break; } // escape

                    ConsoleMessage insult = insults[choice];

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
    }
    static void CarlAttack()
    {
        int choice = rand.Next(0, 101);
        if (choice < 50) // 50% chance enemy atttacks
        {
            switch (rand.Next(0, 4))
            {
                case 0: // sword
                    Narrate("Carl attempts to strike you with his sword.", sleep: 55);
                    if (rand.Next(0, 11) < 8) { Narrate("He missed."); }
                    else
                    {
                        Narrate("He hits! Inflicting a critical wound!", sleep: 55);
                        AddToHealth("player", -50); // 50% attacks * 25% uses sword * 20% lands hit = 2.5% chance he hits you
                    }
                    break;
                case 1: // gold
                    Narrate("Carl hurls a handful of gold coins at you.", sleep: 55);
                    Player.Say("Ouch!");
                    AddToHealth("player", -10);
                    break;

                case 2: // banana
                    Narrate("Carl lobs a bunch of bananas at you.", sleep: 55);
                    Player.Say("ARGH!");
                    Narrate("§(12)CRITICAL HIT!", sleep: 55);
                    AddToHealth("player", -30);
                    break;

                case 3: // canonball
                    Narrate("Carl attempts to lift a canonball to throw at you.", sleep: 55);
                    Narrate("The ball slips out of his hands, landing on his feet and stunning him.", sleep: 55);
                    Narrate("§(10)Carl is distracted for 1 round!", sleep: 55);
                    carlDistracted = true; break;
            }
        }

        else if (choice < 75) // 25% chance he uses an item
        {
            switch (rand.Next(0, 3))
            {
                case 0: // banana
                    Narrate("Carl eats a banana.", sleep: 55);
                    carl.Say("Mmm! Delicious!");
                    AddToHealth("carl", 20);
                    break;
                case 1: // banana
                    Narrate("Carl... Eats some gold coins?", sleep: 55);
                    carl.Say("Ouch!");
                    AddToHealth("carl", -10);
                    break;
                case 2: // bandana
                    Narrate("Carl reveals from his pockets... a bandana.", sleep: 55);
                    Narrate("It had a maths equation written on it. You can't stop yourself from solving it.", sleep: 55);
                    Narrate("§(12)You are distracted for 1 round!", sleep: 55);
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
                Narrate("That one hit a little too close to home. You begin crying.", sleep: 55);
                AddToHealth("player", -10);
            }
            else
            {
                Narrate("Carl's insult was so laughably bad that your charisma went up.", sleep: 55);
                AddCharisma(20);
                AddToHealth("player", 20);
            }
        }
    }
    static void WalkThePlank()
    {
        Person captain = new Person("Captain Blackbeard", ConsoleColor.DarkRed, null);

        captain.Say("WALK THE PLANK YOU RAPSCALLION!");
        carl.Say("Dead men tell no tales!");
        Player.Say("Please! Don't make me do it!");
        captain.Say("Any last words?");
        Player.Say("");
        ReadChars(10);
        captain.Say("DONT REMEMBER ASKING!", sleep: 20);
        Thread.Sleep(500);
        Narrate("The captain shoves you onto the plank and forces you to walk.");
        if (Choose(Build("Walk the plank", "No, I don't want to")) == 1)
        {
            Player.Say("Actually I have rights.");
            Player.Say("Can I talk to your manager?");
            captain.Say("Ay, maties, it's not worth it. ABANDON SHIP!");
            Narrate("The crew abandons ship. You win.");
            Thread.Sleep(500);
            throw new WonGameException(3);
        }
        Narrate("You walk the plank.");
        Narrate("You fall.");
        Narrate("You survive.");
        Narrate("With a minor major interior exterior concussion.");
        AddCharisma(-50);
        Thread.Sleep(500);
        throw new NewGameException();
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
        Narrate($"§(10)Time taken§(15): {totalTime}");
        switch (route)
        {
            case 0:
                Narrate("§(15)Times fallen off a boat: 1");
                Narrate("§(15)Times saved by the bystander watching you get pushed off the boat: 1");
                break;

            case 1:
                Narrate($"§(15)Times been scammed: 1");
                if (knowledge.Contains("Failed map"))
                {
                    Narrate("§(15)Times failed to read a map: 1");
                }
                if (knowledge.Contains("Evaded taxes"))
                {
                    Narrate("§(15)Taxes evaded: all of them");
                }
                Narrate("§(15)Boat leaks fixed: 1");
                Narrate("§(15)Automotive warranties extended: 1");
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
        ClearKeyBuffer();
        Print("§(8)Press any key to exit.");
        Console.ReadKey();

        MainConsole.Clear();
    }
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
                route = e.Route;
                ResultsScreen(route);
            }
            catch (ExitGameException)
            {
                stillPlaying = false;
            }
        }

        // save game / score?
    }
}