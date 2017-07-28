﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Microsoft.Xna.Framework.Audio;
using StardewModdingAPI;
using StardewValley;

/*
TODO:
0. Add in event handling so that I don;t mute a heart event or wedding music.
6. add in Stardew songs again to music selection    
7. add in more tracks.
11. Tutorial for adding more music into the game?
15.add in blank templates for users to make their own wave/sound banks

*/
namespace Omegasis.StardewSymphony
{



    public class Class1 : Mod
    {
       public static string[] subdirectoryEntries = new string[9999999];
        public static string[] fileEntries = new string[9999999];

        public static  List<MusicManager> master_list; //holds all of my WAVE banks and sound banks and their locations.
        public static Dictionary<string, MusicManager> song_wave_reference; //holds a list of all of the cue names that I ever add in.
        public static List<GameLocation> location_list; //holds all of the locations in SDV
        public static List<Cue> temp_cue; //temporary list of songs from music pack

        public static Dictionary<string, string> music_packs;

        public static string master_path; //path to this mod

        static int delay_time;
        static int delay_time_min; //min time to pass before next song
        static int delay_time_max; //max time to pass before next song
        static bool game_loaded; //make sure the game is loaded
        bool silent_rain; //auto mix in SDV rain sound with music?
        int night_time; //not really used, but keeping it for now.
        public static bool seasonal_music; //will I play the seasonal music or not?
        public static Random random;


        static bool no_music; //will trigger if a music pack can't be loaded for that location. 

        public static SoundBank old_sound_bank; //game's default sound bank
        public static SoundBank new_sound_bank;
        public static Cue cueball; //the actual song that is playing. Why do you call songs cues microsoft? Probably something I'm oblivious to.

        public static WaveBank oldwave;
        public static WaveBank newwave;

        public bool once;

        public static MusicManager current_info_class;


        public static bool farm_player;
        public static bool is_farm;

        public override void Entry(IModHelper helper)
        {
            StardewModdingAPI.Events.SaveEvents.AfterLoad+= PlayerEvents_LoadedGame;
            StardewModdingAPI.Events.TimeEvents.DayOfMonthChanged += TimeEvents_DayOfMonthChanged;
            StardewModdingAPI.Events.GameEvents.UpdateTick += GameEvents_UpdateTick;
            StardewModdingAPI.Events.LocationEvents.CurrentLocationChanged += LocationEvents_CurrentLocationChanged;
            once = true;
            MusicHexProcessor.allsoundBanks = new List<string>();
            MusicHexProcessor.allHexDumps = new List<string>();
            MusicHexProcessor.allWaveBanks = new List<string>();
        }

        public void GameEvents_UpdateTick(object sender, EventArgs e)
        {
            if (game_loaded == false) return;
            if (master_list != null)
            {
                if (master_list.Count == 0) return; //basically if absolutly no music is loaded into the game for locations/festivals/seasons, don't override the game's default music player.

            }
            if (cueball == null)
            {
                no_music = true;
                return; //if there wasn't any music at loaded at all for the area, just play the default stardew soundtrack.
            }
            if (no_music == true && cueball.IsPlaying == false)
            {
                cueball = null; //if there was no music loaded for the area and the last song has finished playing, default to the Stardew Soundtrack.
            }
            if (cueball != null)
            {
                no_music = false;
                if (cueball.IsPlaying == false) //if a song isn't playing
                {
                    //cueball = null;
                    if (aTimer.Enabled == false) //if my timer isn't enabled, set it.
                    {
                        SetTimer();
                    }
                    else
                    {
                        //do nothing
                    }
                }
            }

            if (StardewValley.Game1.isFestival() == true)
            {
                return; //replace with festival
            }
            if (StardewValley.Game1.eventUp == true)
            {
                return; //replace with event music
            }

            if (StardewValley.Game1.isRaining == true)
            {
                if (silent_rain == false) return;// If silent_rain = false. AKA, play the rain ambience soundtrack. If it is true, turn off the rain  ambience sound track.
            }

            Game1.currentSong.Stop(AudioStopOptions.Immediate); //stop the normal songs from playing over the new songs
            Game1.nextMusicTrack = "";  //same as above line

        }
        public void TimeEvents_DayOfMonthChanged(object sender, StardewModdingAPI.Events.EventArgsIntChanged e)
        {
            if (game_loaded == false) return;
            random.Next();
            stop_sound(); //if my music player is called and I forget to clean up sound before hand, kill the old sound.
            DataLoader();
            MyWritter();

            if (game_loaded == false) return;
            night_time = Game1.getModeratelyDarkTime();  //not sure I even really use this...
            music_selector();

           
        }
        public void PlayerEvents_LoadedGame(object sender, EventArgs e)
        {
            DataLoader();
            MyWritter();

            music_packs = new Dictionary<string, string>();
            random = new Random();
            master_list = new List<MusicManager>();
            song_wave_reference = new Dictionary<string, MusicManager>();
            location_list = new List<GameLocation>();
            temp_cue = new List<Cue>();
            no_music = true;

            master_creator(); //creates the directory and files necessary to run the mod.
            Location_Grabber(); //grab all of the locations in the game and add them to a list;
            ProcessDirectory(master_path);
            //master_list.Add(new MusicManager("Wave Bank2", "Sound Bank2", PathOnDisk)); Old static way that only alowed one external wave bank. Good thing I found a way around that.
            aTimer.Enabled = false;
            night_time = Game1.getModeratelyDarkTime();
            process_music_packs();


            MusicHexProcessor.processHex();



            Monitor.Log("READY TO GO");
            game_loaded = true;
            music_selector();

        }
        public void LocationEvents_CurrentLocationChanged(object sender, StardewModdingAPI.Events.EventArgsCurrentLocationChanged e)
        {
            if (game_loaded == false) return;
            // Monitor.Log("NEW LOCATION");
            if (Game1.player.currentLocation.name == "Farm") is_farm = true;
            else is_farm = false;
            music_selector();

        }

        public static Timer aTimer = new Timer();
        public void SetTimer()
        {
            //set up a new timer
            Random random2 = new Random();
            delay_time = random2.Next(delay_time_min, delay_time_max); //random number between 0 and n. 0 not included

            // Create a timer with a two second interval.
            aTimer = new System.Timers.Timer(delay_time);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.Enabled = true;
        }
        public void OnTimedEvent(System.Object source, ElapsedEventArgs e)
        {
        //when my timer runs out play some music
            music_selector();
            aTimer.Enabled = false;
        }


        public void master_creator()
        {
            //loads the data to the variables upon loading the game.
            var music_path = Helper.DirectoryPath;
            if (!Directory.Exists(Path.Combine(music_path, "Music_Packs")))
            {
                Monitor.Log("Creating Music Directory");
                Directory.CreateDirectory(Path.Combine(music_path, "Music_Packs")); //create the Music_Packs directory. Because organization is nice.
            }

            /*

            Old chunk of code that was suppose to automatically populate the music packs with a blank music pack for people to use as a template. Sadly this doesn't work all the way.

            if (!Directory.Exists(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack")))
            {
                Monitor.Log("Creating Music Directory");
                Directory.CreateDirectory(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack")); //create the Music_Packs directory. Because organization is nice.
                Setup_Creator(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack", "Config.txt"));
                Setup_Creator(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack", "master_reference_sheet.txt"));
                File.Create(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack", "your_sound_bank_here.xsb"));
                File.Create(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack", "your_wave_bank_here.xwb"));
            }
            

            if (!Directory.Exists(Path.Combine(music_path, "Music_Packs","Blank_Music_Pack","Music_Files", "Seasons")))
            {
                Monitor.Log("Creating Music Directory");
                Directory.CreateDirectory(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack","Music_Files", "Seasons")); //create the Music_Packs directory. Because organization is nice.
            }
            if (!Directory.Exists(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack","Music_Files", "Locations")))
            {
                Monitor.Log("Creating Music Directory");
                Directory.CreateDirectory(Path.Combine(music_path, "Music_Packs", "Blank_Music_Pack","Music_Files", "Locations")); //create the Music_Packs directory. Because organization is nice.
            }
            */

            music_path = Path.Combine(music_path, "Music_Packs");
            master_path = music_path;
            old_sound_bank = Game1.soundBank;

            oldwave = Game1.waveBank;

        } //works


        public static void Info_Loader(string root_dir, string config_path) //reads in cue names from a text file and adds them to a specific list. Morphs with specific conditional name.
        {

            if (!File.Exists(config_path)) //check to make sure the file actually exists. It should.
            {
                Console.WriteLine("StardewSymphony:This music pack lacks a Config.txt. Without one, I can't load in the music.");
                //Setup_Creator(config_path);
            }

            else
            {
                // Load in all of the text files from the Music Packs
                string[] readtext = File.ReadAllLines(config_path);
                string wave = Convert.ToString(readtext[3]);
                string sound = Convert.ToString(readtext[5]);
                MusicManager lol = new MusicManager(wave,sound, root_dir);
                lol.Music_Loader_Seasons("spring", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("summer", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("fall", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("winter", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("spring_night", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("summer_night", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("fall_night", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("winter_night", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("spring_rain", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("summer_rain", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("fall_rain", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("winter_snow", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("spring_rain_night", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("summer_rain_night", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("fall_rain_night", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                lol.Music_Loader_Seasons("winter_snow_night", song_wave_reference); //load all of the info files here. This is some deep magic I worked at 4 AM. I almost forgot how the heck this worked when I woke up.
                foreach (var loc in location_list)
                {
                    lol.Music_Loader_Locations(loc.name, song_wave_reference); //name of location, and the song_wave_reference list
                    lol.Music_Loader_Locations_Night(loc.name+"_night", song_wave_reference); //name of location, and the song_wave_reference list
                    lol.Music_Loader_Locations_Rain(loc.name+"_rain", song_wave_reference); //name of location, and the song_wave_reference list
                    lol.Music_Loader_Locations_Rain_Night(loc.name+"_rain_night", song_wave_reference); //name of location, and the song_wave_reference list
                }
                if (lol != null)
                {
                    master_list.Add(lol); //add everything to my master list of songs!
                }
            }
        }

        public static void ProcessDirectory(string targetDirectory)
        {
           // System.Threading.Thread.Sleep(1);
            // Process the list of files found in the directory.
            fileEntries = Directory.GetFiles(targetDirectory);

            foreach (var v in fileEntries)
            {
                string extension = Path.GetExtension(v);
                // Log.AsyncC(extension);
                if (extension == ".xsb")
                {
                    Log.AsyncG(v);
                    MusicHexProcessor.allsoundBanks.Add(v);
                }
                if(extension == "xwb")
                {
                    Log.AsyncC(v);
                    MusicHexProcessor.allWaveBanks.Add(v);
                }
            }

            if (File.Exists(Path.Combine(targetDirectory, "Config.txt"))){
                string temp = Path.Combine(targetDirectory, "Config.txt");
                //Monitor.Log("YAY");
                music_packs.Add(targetDirectory, temp);
                
            }

           
            
            //do checking for spring, summer, night, etc.

            // Recurse into subdirectories of this directory.
            subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
            {
             
                ProcessDirectory(subdirectory);
            }
            }

        public static void process_music_packs()
        {
            foreach(var hello in music_packs)
            {
                Info_Loader(hello.Key, hello.Value);
               // Monitor.Log("HORRAY");
            }


        }
        public void Location_Grabber()
        {
            //grab each location in SDV so that the mod will update itself accordingly incase new areas are added
            foreach (var loc in StardewValley.Game1.locations)
            {
                Monitor.Log(loc.name);
                location_list.Add(loc);

            }
        }
        public void music_selector()
        {
            if (game_loaded == false)
            {
                return;
            }
          //  no_music = false;
            //if at any time the music for an area can't be played for some unknown reason, the game should default to playing the Stardew Valley Soundtrack.
            bool night_time=false;
            bool rainy = Game1.isRaining;

            if (is_farm == true)
            {
                farm_music_selector();
                return;
            }
            if (StardewValley.Game1.isFestival() == true)
            {
                stop_sound();
                return; //replace with festival music if I decide to support it.
            }
            if (StardewValley.Game1.eventUp == true)
            {
                stop_sound();
                return; //replace with event music if I decide to support it/people request it.
            }


            if (Game1.timeOfDay < 600 || Game1.timeOfDay > Game1.getModeratelyDarkTime())
            {
                night_time = true;
            }
            else
            {
                night_time = false;
            }

            if (rainy == true && night_time == true)
            {
                music_player_rain_night(); //some really awful heirarchy type thing I made up to help ensure that music plays all the time
                if (no_music==true)
                {
                    music_player_rain();
                    if (no_music==true)
                    {
                        music_player_night();
                        if (no_music == true)
                        {
                            music_player_location();
                           
                        }
                    }
                }
          
            }
            if (rainy == true && night_time == false)
            {
                music_player_rain();
                if (no_music == true)
                {
                    music_player_night();
                    if (no_music == true)
                    {
                        music_player_location();
            
                    }
                }

            }
            if (rainy == false && night_time == true)
            {
                music_player_night();
                if (no_music == true) //if there is no music playing right now play some music.
                {
                    music_player_location();
                
                }
                
            }
            if (rainy == false && night_time == false)
            {
                music_player_location();
            }

            if (no_music==false) //if there is valid music playing
            {
               // Monitor.Log("RETURN");
                return;
            }
            else
            {
                if (seasonal_music == false)
                {
                    return;
                }

                if (cueball != null)
                {
                    if (cueball.IsPlaying == true)
                    {
                        return;
                    }
                }

                Monitor.Log("Loading Default Seasonal Music");
                
                if (master_list.Count == 0)
                {
                    Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                    reset();
                    return;

                }

                //add in seasonal stuff here
                if (Game1.IsSpring == true && no_music==true)
                {
                    if (rainy==true)
                    {
                        spring_rain_songs();
                    }
                    else
                    {
                      spring_songs();
                    }
                }
                if (Game1.IsSummer == true && no_music == true)
                {
                    if (rainy == true)
                    {
                        summer_rain_songs();
                    }
                    else
                    {
                        summer_songs();
                    }
                }
                if (Game1.IsFall == true && no_music == true)
                {
                    if (rainy == true)
                    {
                        fall_rain_songs();
                    }
                    else
                    {
                        fall_songs();
                    }
                }
                if (Game1.IsWinter == true && no_music == true)
                {
                    if (Game1.isSnowing==true)
                    {
                       winter_snow_songs();
                    }
                    else
                    {
                        winter_songs();
                    }
                }
                
            }


            //end of function. Natural return;
            return;
        }


        public void farm_music_selector()
        {
            
            if (game_loaded == false)
            {
                return;
            }
            //  no_music = false;
            //if at any time the music for an area can't be played for some unknown reason, the game should default to playing the Stardew Valley Soundtrack.
            bool night_time = false;
            bool rainy = Game1.isRaining;

            Monitor.Log("Loading farm music.");
            if (StardewValley.Game1.isFestival() == true)
            {
                stop_sound();
                return; //replace with festival music if I decide to support it.
            }
            if (StardewValley.Game1.eventUp == true)
            {
                stop_sound();
                return; //replace with event music if I decide to support it/people request it.
            }


            if (Game1.timeOfDay < 600 || Game1.timeOfDay > Game1.getModeratelyDarkTime())
            {
                night_time = true;
            }
            else
            {
                night_time = false;
            }

                Monitor.Log("Loading Default Seasonal Music");

                if (master_list.Count == 0)
                {
                    Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                    reset();
                    return;

                }

                //add in seasonal stuff here
                if (Game1.IsSpring == true)
                {
                    if (rainy == true)
                    {
                        spring_rain_songs();
                    }
                    else
                    {
                        spring_songs();
                    }
                }
                if (Game1.IsSummer == true)
                {
                    if (rainy == true)
                    {
                        summer_rain_songs();
                    }
                    else
                    {
                        summer_songs();
                    }
                }
                if (Game1.IsFall == true)
                {
                    if (rainy == true)
                    {
                        fall_rain_songs();
                    }
                    else
                    {
                        fall_songs();
                    }
                }
                if (Game1.IsWinter == true)
                {
                    if (Game1.isSnowing == true)
                    {
                        winter_snow_songs();
                    }
                    else
                    {
                        winter_songs();
                    }
                }
                //end seasonal songs
            if (cueball != null)
            {
                if (cueball.IsPlaying == true)
                {
                    return;
                }
            }
            //start locational songs
            if (rainy == true && night_time == true)
                {
                    music_player_rain_night(); //some really awful heirarchy type thing I made up to help ensure that music plays all the time
                    if (no_music == true)
                    {
                        music_player_rain();
                        if (no_music == true)
                        {
                            music_player_night();
                            if (no_music == true)
                            {
                                music_player_location();

                            }
                        }
                    }

                }
                if (rainy == true && night_time == false)
                {
                    music_player_rain();
                    if (no_music == true)
                    {
                        music_player_night();
                        if (no_music == true)
                        {
                            music_player_location();

                        }
                    }

                }
                if (rainy == false && night_time == true)
                {
                    music_player_night();
                    if (no_music == true)
                    {
                        music_player_location();

                    }

                }
                if (rainy == false && night_time == false)
                {
                    music_player_location();
                }

            //end of function. Natural return;
            return;
        }

        public void music_player_location()
        {
            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            
            if (Game1.player.currentLocation != null)
            {
                int helper1 = 0;
                int master_helper = 0;
                bool found = false;

                int chedar = 0;
                //this mess of a while loop iterates across all of my music packs looking for a valid music pack to play music from.
                while (true)
                {
                    if (current_info_class.locational_songs.Keys.Contains(Game1.player.currentLocation.name))
                    {

                        foreach (var happy in current_info_class.locational_songs)
                        {
                            if (happy.Key == Game1.player.currentLocation.name)
                            {
                                if (happy.Value.Count > 0)
                                {
                                    //Monitor.Log("FOUND THE RIGHT POSITIONING OF THE CLASS");
                                    found = true;
                                    break;
                                }
                                else
                                {
                                    //this section tells me if it is valid and is less than or equal to 0
                                    //Monitor.Log("Count is less than for this class zero. Switching music packs");
                                    found = false;
                                    master_helper++; //iterate across the classes
                                    break;
                                }

                            }
                            else
                            {//this section iterates through the keys
                                Monitor.Log("Not there");
                                found = false;
                                helper1++;
                                continue;
                            }

                        } //itterate through all of the valid locations that were stored in this class

                    }
                    else
                    {
                        Monitor.Log("No data could be loaded on this area. Swaping music packs");
                        found = false;
                    }
                    if (found == false) //if I didnt find the music.
                    {
                        master_helper++;

                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;

                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                    else
                    {
                        break;
                    }

                }
                temp_cue = current_info_class.locational_songs.Values.ElementAt(helper1); //set a list of songs to a "random" list of songs from a music pack
                int pointer = 0;
                int motzy = 0; //why do I name my variables pointless names?
                while (temp_cue.Count == 0) //yet another circular array
                {
                    pointer++;
                        motzy = (pointer + randomNumber) % master_list.Count;
                    
                    current_info_class = master_list.ElementAt(motzy);
                    if (pointer > master_list.Count)
                    {
                        Monitor.Log("No music packs have any valid music for this area. AKA all music packs are empty;");
                        no_music = true;
                        return;
                    }

                }

                Monitor.Log("loading music for this area");
                if (temp_cue == null)
                {
                    Monitor.Log("temp cue list is null????");
                    return;
                }
                stop_sound();
                int random3 = random.Next(0, temp_cue.Count);
                Game1.soundBank = current_info_class.new_sound_bank; //change the game's soundbank temporarily
                Game1.waveBank = current_info_class.newwave;//dito but wave bank

                cueball = temp_cue.ElementAt(random3); //grab a random song from the winter song list
                cueball = Game1.soundBank.GetCue(cueball.Name);
                if (cueball != null)
                {
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + "for the location " + Game1.player.currentLocation);
                    no_music = false;
                    cueball.Play(); //play some music
                    reset();
                    return;
                }
            }
            else
            {
                Monitor.Log("Location is null");
                no_music = true;
            }
        }//end music player
        public void music_player_rain()
        {
            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            

            if (Game1.player.currentLocation != null)
            {
                int helper1 = 0;
                int master_helper = 0;
                bool found = false;

                int chedar = 0;

                while (true)
                {
                    if (current_info_class.locational_rain_songs.Keys.Contains(Game1.player.currentLocation.name+"_rain"))
                    {

                        foreach (var happy in current_info_class.locational_rain_songs)
                        {
                            if (happy.Key == Game1.player.currentLocation.name+"_rain")
                            {
                                if (happy.Value.Count > 0)
                                {
                                    //Monitor.Log("FOUND THE RIGHT POSITIONING OF THE CLASS");
                                    found = true;
                                    break;
                                }
                                else
                                {
                                    //this section tells me if it is valid and is less than or equal to 0
                                    //Monitor.Log("Count is less than for this class zero. Switching music packs");
                                    found = false;
                                    master_helper++; //iterate across the classes
                                    break;
                                }

                            }
                            else
                            {//this section iterates through the keys
                                Monitor.Log("Not there");
                                found = false;
                                helper1++;
                                continue;
                            }

                        } //itterate through all of the svalid locations that were stored in this class

                    }
                    else
                    {
                        Monitor.Log("No data could be loaded on this area. Swaping music packs");
                        found = false;
                    }
                    if (found == false) //if I didnt find the music.
                    {
                        master_helper++;

                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;

                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                temp_cue = current_info_class.locational_rain_songs.Values.ElementAt(helper1);


                int pointer = 0;
                int motzy = 0;
                while (temp_cue.Count == 0)
                {
                    pointer++;
                    motzy = (pointer + randomNumber) % master_list.Count;

                    current_info_class = master_list.ElementAt(motzy);
                    if (pointer > master_list.Count)
                    {
                        Monitor.Log("No music packs have any valid music for this area. AKA all music packs are empty;");
                        no_music = true;
                        return;
                    }

                }



                Monitor.Log("loading music for this area");
                if (temp_cue == null)
                {
                    Monitor.Log("temp cue list is null????");
                    return;
                }
                stop_sound();
                int random3 = random.Next(0, temp_cue.Count);
                Game1.soundBank = current_info_class.new_sound_bank;
                Game1.waveBank = current_info_class.newwave;

                cueball = temp_cue.ElementAt(random3); //grab a random song from the winter song list
                cueball = Game1.soundBank.GetCue(cueball.Name);
                if (cueball != null)
                {
                    no_music = false;
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + "for the location " + Game1.player.currentLocation + " while it is raining");
                    cueball.Play();
                    reset();
                    return;
                }



            }
            else
            {
                Monitor.Log("Location is null");
            }
        }//end music player
        public void music_player_night()
        {
            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            

            if (Game1.player.currentLocation != null)
            {
                int helper1 = 0;
                int master_helper = 0;
                bool found = false;

                int chedar = 0;

                while (true)
                {
                    if (current_info_class.locational_night_songs.Keys.Contains(Game1.player.currentLocation.name+"_night"))
                    {

                        foreach (var happy in current_info_class.locational_night_songs)
                        {
                            if (happy.Key == Game1.player.currentLocation.name+"_night")
                            {
                                if (happy.Value.Count > 0)
                                {
                                    //Monitor.Log("FOUND THE RIGHT POSITIONING OF THE CLASS");
                                    found = true;
                                    break;
                                }
                                else
                                {
                                    //this section tells me if it is valid and is less than or equal to 0
                                    //Monitor.Log("Count is less than for this class zero. Switching music packs");
                                    found = false;
                                    master_helper++; //iterate across the classes
                                    break;
                                }

                            }
                            else
                            {//this section iterates through the keys
                                Monitor.Log("Not there");
                                found = false;
                                helper1++;
                                continue;
                            }

                        } //itterate through all of the svalid locations that were stored in this class

                    }
                    else
                    {
                        Monitor.Log("No data could be loaded on this area. Swaping music packs");
                        found = false;
                    }
                    if (found == false) //if I didnt find the music.
                    {
                        master_helper++;

                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;

                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                temp_cue = current_info_class.locational_night_songs.Values.ElementAt(helper1);
                int pointer = 0;
                int motzy = 0;
                while (temp_cue.Count == 0)
                {
                    pointer++;
                    motzy = (pointer + randomNumber) % master_list.Count;

                    current_info_class = master_list.ElementAt(motzy);
                    if (pointer > master_list.Count)
                    {
                        Monitor.Log("No music packs have any valid music for this area. AKA all music packs are empty;");
                        no_music = true;
                        return;
                    }

                }


                Monitor.Log("loading music for this area");
                if (temp_cue == null)
                {
                    Monitor.Log("temp cue list is null????");
                    return;
                }
                stop_sound();
                int random3 = random.Next(0, temp_cue.Count);
                Game1.soundBank = current_info_class.new_sound_bank;
                Game1.waveBank = current_info_class.newwave;

                cueball = temp_cue.ElementAt(random3); //grab a random song from the winter song list
                cueball = Game1.soundBank.GetCue(cueball.Name);
                if (cueball != null)
                {
                    no_music = false;
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + "for the location " + Game1.player.currentLocation + " while it is night time.");
                    cueball.Play();
                    reset();
                    return;
                }



            }
            else
            {
                Monitor.Log("Location is null");
            }
        }//end music player
        public void music_player_rain_night()
        {
            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            

            if (Game1.player.currentLocation != null)
            {
               
                int helper1 = 0;
                int master_helper = 0;
                bool found = false;

                int chedar = 0; //this is why I shouldn't program before a date. I name my variables after really random crap.

                while (true)
                {
                    if (current_info_class.locational_rain_night_songs.Keys.Contains(Game1.player.currentLocation.name+"_rain_night"))
                    {

                        foreach (var happy in current_info_class.locational_rain_night_songs)
                        {
                            if (happy.Key == Game1.player.currentLocation.name+"_rain_night")
                            {
                                if (happy.Value.Count > 0)
                                {
                                    //Monitor.Log("FOUND THE RIGHT POSITIONING OF THE CLASS");
                                    found = true;
                                    break;
                                }
                                else
                                {
                                    //this section tells me if it is valid and is less than or equal to 0
                                    //Monitor.Log("Count is less than for this class zero. Switching music packs");
                                    found = false;
                                    master_helper++; //iterate across the classes
                                    break;
                                }

                            }
                            else
                            {//this section iterates through the keys
                                Monitor.Log("Not there");
                                found = false;
                                helper1++;
                                continue;
                            }

                        } //itterate through all of the svalid locations that were stored in this class

                    }
                    else
                    {
                        Monitor.Log("No data could be loaded on this area. Swaping music packs");
                        found = false;
                    }
                    if (found == false) //if I didnt find the music.
                    {
                        master_helper++;

                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;

                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                temp_cue = current_info_class.locational_rain_night_songs.Values.ElementAt(helper1);

                int pointer = 0;
                int motzy = 0;
                while (temp_cue.Count == 0)
                {
                    pointer++;
                    motzy = (pointer + randomNumber) % master_list.Count;

                    current_info_class = master_list.ElementAt(motzy);
                    if (pointer > master_list.Count)
                    {
                        Monitor.Log("No music packs have any valid music for this area. AKA all music packs are empty;");
                        no_music = true;
                        return;
                    }

                }
                Monitor.Log("loading music for this area");
                if (temp_cue == null)
                {
                    Monitor.Log("temp cue list is null????");
                    return;
                }
                stop_sound();
                int random3 = random.Next(0, temp_cue.Count);
                Game1.soundBank = current_info_class.new_sound_bank;
                Game1.waveBank = current_info_class.newwave;

                cueball = temp_cue.ElementAt(random3); //grab a random song from the winter song list
                cueball = Game1.soundBank.GetCue(cueball.Name);
                if (cueball != null)
                {
                    no_music = false;
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + "for the location " + Game1.player.currentLocation + " while it is raining at night.");
                    cueball.Play();
                    reset();
                    return;
                }



            }
            else
            {
                Monitor.Log("Location is null");
            }
        }//end music player

        public void spring_songs()
        {

            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            

           
            if (Game1.timeOfDay < 600 || Game1.timeOfDay >= Game1.getModeratelyDarkTime())  //expanded upon, just incase my night owl mod is installed.
            {
                randomNumber = random.Next(0,current_info_class.num_of_spring_night_songs); //random number between 0 and n. 0 not includes

                if (current_info_class.spring_night_song_list.Count == 0) //nightly spring songs
                {
                    Monitor.Log("The spring night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                    int master_helper = 0;
                    int chedar = 0;
                    while (master_helper != master_list.Count)
                    {
                        if (current_info_class.spring_night_song_list.Count > 0)
                        {
                            stop_sound();
                            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                            Game1.waveBank = current_info_class.newwave;
                            cueball = current_info_class.spring_night_song_list.ElementAt(randomNumber); //grab a random song from the spring song list
                            cueball = Game1.soundBank.GetCue(cueball.Name);
                            break;

                        } //itterate through all of the svalid locations that were stored in this class
                        else
                        {
                            master_helper++;
                        }
                        if (master_helper > master_list.Count)
                            {
                                Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                                no_music = true;
                           
                            return;
                          
                            //break;
                            }
                            chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                            current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                            continue;
                        }
                }


                else
                {
                    stop_sound();
                    cueball = current_info_class.spring_night_song_list.ElementAt(randomNumber); //grab a random song from the spring song list
                    Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                    Game1.waveBank = current_info_class.newwave;
                    cueball = Game1.soundBank.GetCue(cueball.Name);
                }
                if (cueball != null)
                {
                    no_music = false;
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a Spring Night. Check the seasons folder for more info");
                    cueball.Play();
                    Class1.reset();
                    return;
                } //if cueballs is null, aka the song list either wasn't initialized, or it is empty, default to playing the normal songs.


            }
            //not nightly spring songs. AKA default songs

            randomNumber = random.Next(0,current_info_class.num_of_spring_songs); //random number between 0 and n. 0 not includes
            if (current_info_class.spring_song_list.Count == 0)
            {
                Monitor.Log("The spring night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                int master_helper = 0;
                int chedar = 0;
                while (master_helper != master_list.Count)
                {
                    if (current_info_class.spring_night_song_list.Count > 0)
                    {
                        stop_sound();
                        Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                        Game1.waveBank = current_info_class.newwave;
                        cueball = current_info_class.spring_song_list.ElementAt(randomNumber); //grab a random song from the spring song list
                        cueball = Game1.soundBank.GetCue(cueball.Name);
                        break;

                    } //itterate through all of the svalid locations that were stored in this class
                    else
                    {
                        master_helper++;
                    }
                    if (master_helper > master_list.Count)
                    {
                        Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                        no_music = true;
                        return;
            //            cueball = null;
                    }
                    chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                    current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                    continue;
                }
            }
            else
            {
                stop_sound();
                cueball = current_info_class.spring_song_list.ElementAt(randomNumber); //grab a random song from the spring song list
                Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                Game1.waveBank = current_info_class.newwave;
                cueball = Game1.soundBank.GetCue(cueball.Name);
            }
            if (cueball == null) return;
            no_music = false;
            Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + "while it is Spring Time. Check the seasons folder for more info");
            cueball.Play();
            Class1.reset();
            return;

        } //plays the songs associated with spring time
        public void spring_rain_songs()
        {

            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            


            if (Game1.timeOfDay < 600 || Game1.timeOfDay >= Game1.getModeratelyDarkTime())  //expanded upon, just incase my night owl mod is installed.
            {
                randomNumber = random.Next(0, current_info_class.num_of_spring_rain_night_songs); //random number between 0 and n. 0 not includes

                if (current_info_class.spring_rain_night_song_list.Count == 0) //nightly spring_rain songs
                {
                    Monitor.Log("The spring_rain night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                    int master_helper = 0;
                    int chedar = 0;
                    while (master_helper != master_list.Count)
                    {
                        if (current_info_class.spring_rain_night_song_list.Count > 0)
                        {
                            stop_sound();
                            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                            Game1.waveBank = current_info_class.newwave;
                            cueball = current_info_class.spring_rain_night_song_list.ElementAt(randomNumber); //grab a random song from the spring_rain song list
                            cueball = Game1.soundBank.GetCue(cueball.Name);
                            break;

                        } //itterate through all of the svalid locations that were stored in this class
                        else
                        {
                            master_helper++;
                        }
                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;
                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                }
                else
                {
                    stop_sound();
                    cueball = current_info_class.spring_rain_night_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                    Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                    Game1.waveBank = current_info_class.newwave;
                    cueball = Game1.soundBank.GetCue(cueball.Name);
                }


                if (cueball != null)
                {
                    no_music = false;
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + "while it is a rainy Spring night. Check the Seasons folder for more info");
                    cueball.Play();
                    Class1.reset();
                    return;
                } //if cueballs is null, aka the song list either wasn't initialized, or it is empty, default to playing the normal songs.


            }
            //not nightly spring_rain songs. AKA default songs

            randomNumber = random.Next(0, current_info_class.num_of_spring_rain_songs); //random number between 0 and n. 0 not includes
            if (current_info_class.spring_rain_song_list.Count == 0)
            {
                Monitor.Log("The spring_rain night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                int master_helper = 0;
                int chedar = 0;
                while (master_helper != master_list.Count)
                {
                    if (current_info_class.spring_rain_song_list.Count > 0)
                    {
                        stop_sound();
                        Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                        Game1.waveBank = current_info_class.newwave;
                        cueball = current_info_class.spring_rain_song_list.ElementAt(randomNumber); //grab a random song from the spring_rain song list
                        cueball = Game1.soundBank.GetCue(cueball.Name);
                        break;

                    } //itterate through all of the svalid locations that were stored in this class
                    else
                    {
                        master_helper++;
                    }
                    if (master_helper > master_list.Count)
                    {
                        Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                        no_music = true;
                        return;
                        //            cueball = null;
                    }
                    chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                    current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                    continue;
                }
            }
            else
            {
                stop_sound();
                cueball = current_info_class.spring_rain_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                Game1.waveBank = current_info_class.newwave;
                cueball = Game1.soundBank.GetCue(cueball.Name);
            }
            if (cueball == null) return;
            no_music = false;
            Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + "while it is a rainy Spring Day. Check the seasons folder for more info");
            cueball.Play();
            Class1.reset();
            return;

        } //plays the songs associated with spring time
        public void summer_songs()
        {

            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            


            if (Game1.timeOfDay < 600 || Game1.timeOfDay >= Game1.getModeratelyDarkTime())  //expanded upon, just incase my night owl mod is installed.
            {
                randomNumber = random.Next(0, current_info_class.num_of_summer_night_songs); //random number between 0 and n. 0 not includes

                if (current_info_class.summer_night_song_list.Count == 0) //nightly summer songs
                {
                    Monitor.Log("The summer night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                    int master_helper = 0;
                    int chedar = 0;
                    while (master_helper != master_list.Count)
                    {
                        if (current_info_class.summer_night_song_list.Count > 0)
                        {
                            stop_sound();
                            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                            Game1.waveBank = current_info_class.newwave;
                            cueball = current_info_class.summer_night_song_list.ElementAt(randomNumber); //grab a random song from the summer song list
                            cueball = Game1.soundBank.GetCue(cueball.Name);
                            break;

                        } //itterate through all of the svalid locations that were stored in this class
                        else
                        {
                            master_helper++;
                        }
                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;
                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                }
                else
                {
                    stop_sound();
                    Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                    Game1.waveBank = current_info_class.newwave;
                    cueball = current_info_class.summer_night_song_list.ElementAt(randomNumber); //grab a random song from the summer song list
                    cueball = Game1.soundBank.GetCue(cueball.Name);
                    
                }



                if (cueball != null)
                {
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a Summer Night. Check the Seasons folder for more info.");
                    no_music = false;
                    cueball.Play();
                    Class1.reset();
                    return;
                } //if cueballs is null, aka the song list either wasn't initialized, or it is empty, default to playing the normal songs.


            }
            //not nightly summer songs. AKA default songs

            randomNumber = random.Next(0, current_info_class.num_of_summer_songs); //random number between 0 and n. 0 not includes
            if (current_info_class.summer_song_list.Count == 0)
            {
                Monitor.Log("The summer night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                int master_helper = 0;
                int chedar = 0;
                while (master_helper != master_list.Count)
                {
                    if (current_info_class.summer_night_song_list.Count > 0)
                    {
                        stop_sound();
                        Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                        Game1.waveBank = current_info_class.newwave;
                        cueball = current_info_class.summer_song_list.ElementAt(randomNumber); //grab a random song from the summer song list
                        cueball = Game1.soundBank.GetCue(cueball.Name);
                        break;

                    } //itterate through all of the svalid locations that were stored in this class
                    else
                    {
                        master_helper++;
                    }
                    if (master_helper > master_list.Count)
                    {
                        Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                        no_music = true;
                        return;
                        //            cueball = null;
                    }
                    chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                    current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                    continue;
                }
            }
            if (cueball == null) return;
            stop_sound();
            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
            Game1.waveBank = current_info_class.newwave;
            cueball = current_info_class.summer_song_list.ElementAt(randomNumber); //grab a random song from the summer song list
            cueball = Game1.soundBank.GetCue(cueball.Name);
            if (cueball != null)
            {
                Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a Fall day. Check the Seasons folder for more info.");
                // System.Threading.Thread.Sleep(30000);
                no_music = false;
                cueball.Play();
                Class1.reset();
            }      
            return;

        } //plays the songs associated with summer time
        public void summer_rain_songs()
        {

            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            


            if (Game1.timeOfDay < 600 || Game1.timeOfDay >= Game1.getModeratelyDarkTime())  //expanded upon, just incase my night owl mod is installed.
            {
                randomNumber = random.Next(0, current_info_class.num_of_summer_rain_night_songs); //random number between 0 and n. 0 not includes

                if (current_info_class.summer_rain_night_song_list.Count == 0) //nightly summer_rain songs
                {
                    Monitor.Log("The summer_rain night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                    int master_helper = 0;
                    int chedar = 0;
                    while (master_helper != master_list.Count)
                    {
                        if (current_info_class.summer_rain_night_song_list.Count > 0)
                        {
                            stop_sound();
                            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                            Game1.waveBank = current_info_class.newwave;
                            cueball = current_info_class.summer_rain_night_song_list.ElementAt(randomNumber); //grab a random song from the summer_rain song list
                            cueball = Game1.soundBank.GetCue(cueball.Name);
                            break;

                        } //itterate through all of the svalid locations that were stored in this class
                        else
                        {
                            master_helper++;
                        }
                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;
                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                }

                else
                {
                    stop_sound();
                    cueball = current_info_class.summer_rain_night_song_list.ElementAt(randomNumber); //grab a random song from the summer song list
                    Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                    Game1.waveBank = current_info_class.newwave;
                    cueball = Game1.soundBank.GetCue(cueball.Name);
                }

                if (cueball != null)
                {
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a rainy Summer Night. Check the Seasons folder for more info.");
                    no_music = false;
                    cueball.Play();
                    Class1.reset();
                    return;
                } //if cueballs is null, aka the song list either wasn't initialized, or it is empty, default to playing the normal songs.


            }
            //not nightly summer_rain songs. AKA default songs

            randomNumber = random.Next(0, current_info_class.num_of_summer_rain_songs); //random number between 0 and n. 0 not includes
            if (current_info_class.summer_rain_song_list.Count == 0)
            {
                Monitor.Log("The summer_rain night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                int master_helper = 0;
                int chedar = 0;
                while (master_helper != master_list.Count)
                {
                    if (current_info_class.summer_rain_song_list.Count > 0)
                    {
                        stop_sound();
                        Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                        Game1.waveBank = current_info_class.newwave;
                        cueball = current_info_class.summer_rain_song_list.ElementAt(randomNumber); //grab a random song from the summer_rain song list
                        cueball = Game1.soundBank.GetCue(cueball.Name);
                        break;

                    } //itterate through all of the svalid locations that were stored in this class
                    else
                    {
                        master_helper++;
                    }
                    if (master_helper > master_list.Count)
                    {
                        Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                        no_music = true;
                        return;
                        //            cueball = null;
                    }
                    chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                    current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                    continue;
                }
            }

            else
            {
                stop_sound();
                cueball = current_info_class.summer_rain_song_list.ElementAt(randomNumber); //grab a random song from the summer song list
                Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                Game1.waveBank = current_info_class.newwave;
                cueball = Game1.soundBank.GetCue(cueball.Name);
            }

            if (cueball == null) return;
            Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a rainy Summer day. Check the Seasons folder for more info.");
            no_music = false;
            cueball.Play();
            Class1.reset();
            return;

        } //plays the songs associated with summer time
        public void fall_songs()
        {

            if (game_loaded == false)
            {
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
               // System.Threading.Thread.Sleep(3000);
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            


            if (Game1.timeOfDay < 600 || Game1.timeOfDay >= Game1.getModeratelyDarkTime())  //expanded upon, just incase my night owl mod is installed.
            {
                randomNumber = random.Next(0, current_info_class.fall_night_song_list.Count); //random number between 0 and n. 0 not includes

                if (current_info_class.fall_night_song_list.Count == 0) //nightly fall songs
                {
                    Monitor.Log("The fall night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                 //   System.Threading.Thread.Sleep(3000);
                    int master_helper = 0;
                    int chedar = 0;
                    while (master_helper != master_list.Count)
                    {
                        if (current_info_class.fall_night_song_list.Count > 0)
                        {
                            stop_sound();
                            cueball = current_info_class.fall_night_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                            Game1.waveBank = current_info_class.newwave;

                            cueball = Game1.soundBank.GetCue(cueball.Name);
                            break;

                        } //itterate through all of the svalid locations that were stored in this class
                        else
                        {
                            master_helper++;
                        }
                        if (master_helper >= master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                   //         System.Threading.Thread.Sleep(3000);
                            no_music = true;
                            return;
                           // cueball = null;
                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                }
                else
                {
                    stop_sound();

                    cueball = current_info_class.fall_night_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                    Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                    Game1.waveBank = current_info_class.newwave;
                    cueball = Game1.soundBank.GetCue(cueball.Name);
                }


                if (cueball != null)
                {
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a Fall Night. Check the Seasons folder for more info.");

                    //System.Threading.Thread.Sleep(30000);
                    no_music = false;
                    cueball.Play();
                    reset();
                    return;
                } //if cueballs is null, aka the song list either wasn't initialized, or it is empty, default to playing the normal songs.


            }
            //not nightly fall songs. AKA default songs

            randomNumber = random.Next(0, current_info_class.fall_song_list.Count); //random number between 0 and n. 0 not includes
            if (current_info_class.fall_song_list.Count == 0)
            {
                Monitor.Log("The fall night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
               // System.Threading.Thread.Sleep(3000);
                int master_helper = 0;
                int chedar = 0;
                while (master_helper != master_list.Count)
                {
                    if (current_info_class.fall_song_list.Count > 0)
                    {
                        stop_sound();
                        cueball = current_info_class.fall_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                        Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                        Game1.waveBank = current_info_class.newwave;
                        cueball = Game1.soundBank.GetCue(cueball.Name);
                        break;

                    } //itterate through all of the svalid locations that were stored in this class
                    else
                    {
                        master_helper++;
                    }
                
                    if (master_helper >= master_list.Count)
                    {
                        Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                 //       System.Thr1eading.Thread.Sleep(3000);
                        no_music = true;
                        return;
                        //            cueball = null;
                    }
                    chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                    current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                    continue;
                }
            }
            else
            {
                stop_sound();
                cueball = current_info_class.fall_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                Game1.waveBank = current_info_class.newwave;
                cueball = Game1.soundBank.GetCue(cueball.Name);
            }
            if (cueball != null)
            {
                Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a Fall day. Check the Seasons folder for more info.");
               // System.Threading.Thread.Sleep(30000);
                no_music = false;
                cueball.Play();
                reset();
            }
            return;

        } //plays the songs associated with fall time
        public void fall_rain_songs()
        {

            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            


            if (Game1.timeOfDay < 600 || Game1.timeOfDay >= Game1.getModeratelyDarkTime())  //expanded upon, just incase my night owl mod is installed.
            {
                randomNumber = random.Next(0, current_info_class.num_of_fall_rain_night_songs); //random number between 0 and n. 0 not includes

                if (current_info_class.fall_rain_night_song_list.Count == 0) //nightly fall_rain songs
                {
                    Monitor.Log("The fall_rain night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                    int master_helper = 0;
                    int chedar = 0;
                    while (master_helper != master_list.Count)
                    {
                        if (current_info_class.fall_rain_night_song_list.Count > 0)
                        {
                            stop_sound();
                            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                            Game1.waveBank = current_info_class.newwave;
                            cueball = current_info_class.fall_rain_night_song_list.ElementAt(randomNumber); //grab a random song from the fall_rain song list
                            cueball = Game1.soundBank.GetCue(cueball.Name);

                            break;

                        } //itterate through all of the svalid locations that were stored in this class
                        else
                        {
                            master_helper++;
                        }
                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;
                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                }
                else
                {
                    stop_sound();
                    cueball = current_info_class.fall_rain_night_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                    Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                    Game1.waveBank = current_info_class.newwave;
                    cueball = Game1.soundBank.GetCue(cueball.Name);
                }


                if (cueball != null)
                {
                    no_music = false;
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a rainy Fall Night. Check the Seasons folder for more info.");
                    cueball.Play();
                    Class1.reset();
                    return;
                } //if cueballs is null, aka the song list either wasn't initialized, or it is empty, default to playing the normal songs.


            }
            //not nightly fall_rain songs. AKA default songs

            randomNumber = random.Next(0, current_info_class.num_of_fall_rain_songs); //random number between 0 and n. 0 not includes
            if (current_info_class.fall_rain_song_list.Count == 0)
            {
                Monitor.Log("The fall_rain night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                int master_helper = 0;
                int chedar = 0;
                while (master_helper != master_list.Count)
                {
                    if (current_info_class.fall_rain_song_list.Count > 0)
                    {
                        stop_sound();
                        Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                        Game1.waveBank = current_info_class.newwave;
                        cueball = current_info_class.fall_rain_song_list.ElementAt(randomNumber); //grab a random song from the fall_rain song list
                        cueball = Game1.soundBank.GetCue(cueball.Name);
                        break;

                    } //itterate through all of the svalid locations that were stored in this class
                    else
                    {
                        master_helper++;
                    }
                    if (master_helper > master_list.Count)
                    {
                        Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                        no_music = true;
                        return;
                        //            cueball = null;
                    }
                    chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                    current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                    continue;
                }
            }
            else
            {
                stop_sound();
                cueball = current_info_class.fall_rain_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                Game1.waveBank = current_info_class.newwave;
                cueball = Game1.soundBank.GetCue(cueball.Name);
            }

            if (cueball == null) return;
            Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a rainy Fall day. Check the Seasons folder for more info.");
            no_music = false;
            cueball.Play();
            Class1.reset();
            return;

        } //plays the songs associated with fall time
        public void winter_songs()
        {

            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            


            if (Game1.timeOfDay < 600 || Game1.timeOfDay >= Game1.getModeratelyDarkTime())  //expanded upon, just incase my night owl mod is installed.
            {
                randomNumber = random.Next(0, current_info_class.num_of_winter_night_songs); //random number between 0 and n. 0 not includes

                if (current_info_class.winter_night_song_list.Count == 0) //nightly winter songs
                {

                    Monitor.Log("The winter night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                    int master_helper = 0;
                    int chedar = 0;
                    while (master_helper != master_list.Count)
                    {
                        if (current_info_class.winter_night_song_list.Count > 0)
                        {
                            stop_sound();
                            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                            Game1.waveBank = current_info_class.newwave;
                            cueball = current_info_class.winter_night_song_list.ElementAt(randomNumber); //grab a random song from the winter song list
                            cueball = Game1.soundBank.GetCue(cueball.Name);
                            break;

                        } //itterate through all of the svalid locations that were stored in this class
                        else
                        {
                            master_helper++;
                        }
                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;
                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                }

                else
                {
                    stop_sound();
                    cueball = current_info_class.winter_night_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                    Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                    Game1.waveBank = current_info_class.newwave;
                    cueball = Game1.soundBank.GetCue(cueball.Name);
                }

                if (cueball != null)
                {
                    no_music = false;
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a Winter Night. Check the Seasons folder for more info.");
                    cueball.Play();
                    Class1.reset();
                    return;
                } //if cueballs is null, aka the song list either wasn't initialized, or it is empty, default to playing the normal songs.


            }
            //not nightly winter songs. AKA default songs

            randomNumber = random.Next(0, current_info_class.num_of_winter_songs); //random number between 0 and n. 0 not includes
            if (current_info_class.winter_song_list.Count == 0)
            {
                Monitor.Log("The winter night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                int master_helper = 0;
                int chedar = 0;
                while (master_helper != master_list.Count)
                {
                    if (current_info_class.winter_night_song_list.Count > 0)
                    {
                        stop_sound();
                        Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                        Game1.waveBank = current_info_class.newwave;
                        cueball = current_info_class.winter_song_list.ElementAt(randomNumber); //grab a random song from the winter song list
                        cueball = Game1.soundBank.GetCue(cueball.Name);
                        break;

                    } //itterate through all of the svalid locations that were stored in this class
                    else
                    {
                        master_helper++;
                    }
                    if (master_helper > master_list.Count)
                    {
                        Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                        no_music = true;
                        return;
                        //            cueball = null;
                    }
                    chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                    current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                    continue;
                }
            }
            else
            {
                stop_sound();
                cueball = current_info_class.winter_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                Game1.waveBank = current_info_class.newwave;
                cueball = Game1.soundBank.GetCue(cueball.Name);
            }
            if (cueball == null) return;
            Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a Winter Day. Check the Seasons folder for more info.");
            no_music = false;
            cueball.Play();
            Class1.reset();
            return;

        } //plays the songs associated with winter time
        public void winter_snow_songs()
        {

            if (game_loaded == false)
            {
                SetTimer();
                return;
            }
            random.Next();
            int randomNumber = random.Next(0, master_list.Count); //random number between 0 and n. 0 not included

            if (master_list.Count == 0)
            {
                Monitor.Log("The Wave Bank list is empty. Something went wrong, or you don't have any music packs installed, or you didn't have any songs in the list files.");
                reset();
                return;

            }

            current_info_class = master_list.ElementAt(randomNumber); //grab a random wave bank/song bank/music pack/ from all available music packs.            


            if (Game1.timeOfDay < 600 || Game1.timeOfDay >= Game1.getModeratelyDarkTime())  //expanded upon, just incase my night owl mod is installed.
            {
                randomNumber = random.Next(0, current_info_class.num_of_winter_snow_night_songs); //random number between 0 and n. 0 not includes

                if (current_info_class.winter_snow_night_song_list.Count == 0) //nightly winter_snow songs
                {
                    Monitor.Log("The winter_snow night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                    int master_helper = 0;
                    int chedar = 0;
                    while (master_helper != master_list.Count)
                    {
                        if (current_info_class.winter_snow_night_song_list.Count > 0)
                        {
                            stop_sound();
                            Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                            Game1.waveBank = current_info_class.newwave;
                            cueball = current_info_class.winter_snow_night_song_list.ElementAt(randomNumber); //grab a random song from the winter_snow song list
                            cueball = Game1.soundBank.GetCue(cueball.Name);
                            break;

                        } //itterate through all of the svalid locations that were stored in this class
                        else
                        {
                            master_helper++;
                        }
                        if (master_helper > master_list.Count)
                        {
                            Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                            no_music = true;
                            return;
                        }
                        chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                        current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                        continue;
                    }
                }

                else
                {
                    stop_sound();
                    cueball = current_info_class.winter_snow_night_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                    Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                    Game1.waveBank = current_info_class.newwave;
                    cueball = Game1.soundBank.GetCue(cueball.Name);
                }

                if (cueball != null)
                {
                    no_music = false;
                    Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a snowy Winter night. Check the Seasons folder for more info.");
                    cueball.Play();
                    Class1.reset();
                    return;
                } //if cueballs is null, aka the song list either wasn't initialized, or it is empty, default to playing the normal songs.


            }
            //not nightly winter_snow songs. AKA default songs

            randomNumber = random.Next(0, current_info_class.num_of_winter_snow_songs); //random number between 0 and n. 0 not includes
            if (current_info_class.winter_snow_song_list.Count == 0)
            {
                Monitor.Log("The winter_snow night song list is empty. Trying to look for more songs."); //or should I default where if there aren't any nightly songs to play a song from a different play list?
                int master_helper = 0;
                int chedar = 0;
                while (master_helper != master_list.Count)
                {
                    if (current_info_class.winter_snow_song_list.Count > 0)
                    {
                        stop_sound();
                        Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                        Game1.waveBank = current_info_class.newwave;
                        cueball = current_info_class.winter_snow_song_list.ElementAt(randomNumber); //grab a random song from the winter_snow song list
                        cueball = Game1.soundBank.GetCue(cueball.Name);
                        break;

                    } //itterate through all of the svalid locations that were stored in this class
                    else
                    {
                        master_helper++;
                    }
                    if (master_helper > master_list.Count)
                    {
                        Monitor.Log("I've gone though every music pack with no success for default music. There is no music to load for this area so it will be silent once this song finishes playing. Sorry!");
                        no_music = true;
                        return;
                        //            cueball = null;
                    }
                    chedar = (master_helper + randomNumber) % master_list.Count; //circular arrays FTW

                    current_info_class = master_list.ElementAt(chedar); //grab a random wave bank/song bank/music pack/ from all available music packs.            
                    continue;
                }
            }
            else
            {
                stop_sound();
                cueball = current_info_class.winter_snow_song_list.ElementAt(randomNumber); //grab a random song from the fall song list
                Game1.soundBank = current_info_class.new_sound_bank; //access my new sound table
                Game1.waveBank = current_info_class.newwave;
                cueball = Game1.soundBank.GetCue(cueball.Name);
            }
            if (cueball == null) return;
            no_music = false;
            Monitor.Log("Now listening to: " + cueball.Name + " from the music pack located at: " + current_info_class.path_loc + " while it is a snowy winter day. Check the Seasons folder for more info.");
            cueball.Play();
            Class1.reset();
            return;

        } //plays the songs associated with spring time
    

        public static void stop_sound() //Stops any song that is playing at the time.
        {
            
            if (cueball == null)
            {
                //trying to stop a song that doesn't "exist" crashes the game. This prevents that.
                return;
            }

            if (current_info_class == null)
            {
               //if my info class is null, return. Should only be null if the game starts. Pretty sure my code should prevent this.
                return;
            }
            Game1.soundBank = current_info_class.new_sound_bank; //reset the wave/sound banks back to the music pack's
            Game1.waveBank = current_info_class.newwave;
            cueball.Stop(AudioStopOptions.Immediate); //redundant stopping code
            cueball.Stop(AudioStopOptions.AsAuthored);
            Game1.soundBank = old_sound_bank; //reset the wave/sound to the game's original
            Game1.waveBank = oldwave;
           cueball = null;
        } //stop whatever song is playing.
        public static void reset()
        {
            Game1.waveBank = oldwave;
            Game1.soundBank = old_sound_bank;
        }

//config/loading stuff below
        void MyWritter()
        {
            //saves the BuildEndurance_data at the end of a new day;
            string mylocation = Path.Combine(Helper.DirectoryPath, "Music_Expansion_Config");
            //string mylocation2 = mylocation + myname;
            string mylocation3 = mylocation + ".txt";
            string[] mystring3 = new string[20];
            if (!File.Exists(mylocation3))
            {
                Console.WriteLine("The config file for SDV_Music_Expansion wasn't found. Time to create it!");

                //write out the info to a text file at the end of a day. This will run if it doesnt exist.

                mystring3[0] = "Player: Stardew Valley Music Expansion Config. Feel free to edit.";
                mystring3[1] = "====================================================================================";

                mystring3[2] = "Minimum delay time: This is the minimal amout of time(in miliseconds!!!) that will pass before another song will play. 0 means a song will play immediately, 1000 means a second will pass, etc. Used in RNG to determine a random delay between songs.";
                mystring3[3] = delay_time_min.ToString();

                mystring3[4] = "Maximum delay time: This is the maximum amout of time(in miliseconds!!!) that will pass before another song will play. 0 means a song will play immediately, 1000 means a second will pass, etc. Used in RNG to determine a random delay between songs.";
                mystring3[5] = delay_time_max.ToString();

                mystring3[6] = "Silent rain? Setting this value to false plays the default ambient rain music along side whatever songs are set in rain music. Setting this to true will disable the ambient rain music. It's up to the soundpack creators wither or not they want to mix their music with rain prior to loading it in here.";
                mystring3[7] = silent_rain.ToString();

                mystring3[8] = "Seasonal_Music? Setting this value to true will play the seasonal music from the music packs instead of defaulting to the Stardew Valley Soundtrack.";
                mystring3[9] = seasonal_music.ToString();

                mystring3[10] = "Prioritize seasonal music on the Farm? If true the game will check for seasonal music before checking for locational music.";
                mystring3[11] = farm_player.ToString();

                File.WriteAllLines(mylocation3, mystring3);
            }

            else
            {

                //write out the info to a text file at the end of a day. This will run if it doesnt exist.

                mystring3[0] = "Player: Stardew Valley Music Expansion Config. Feel free to edit.";
                mystring3[1] = "====================================================================================";

                mystring3[2] = "Minimum delay time: This is the minimal amout of time(in miliseconds!!!) that will pass before another song will play. 0 means a song will play immediately, 1000 means a second will pass, etc. Used in RNG to determine a random delay between songs.";
                mystring3[3] = delay_time_min.ToString();

                mystring3[4] = "Maximum delay time: This is the maximum amout of time(in miliseconds!!!) that will pass before another song will play. 0 means a song will play immediately, 1000 means a second will pass, etc. Used in RNG to determine a random delay between songs.";
                mystring3[5] = delay_time_max.ToString();

                mystring3[6] = "Silent rain? Setting this value to false plays the default ambient rain music along side whatever songs are set in rain music. Setting this to true will disable the ambient rain music. It's up to the soundpack creators wither or not they want to mix their music with rain prior to loading it in here.";
                mystring3[7] = silent_rain.ToString();
                mystring3[8] = "Seasonal_Music? Setting this value to true will play the seasonal music from the music packs instead of defaulting to the Stardew Valley Soundtrack.";
                mystring3[9] = seasonal_music.ToString();

                mystring3[10] = "Prioritize seasonal music on the Farm? If true the game will check for seasonal music before checking for locational music.";
                mystring3[11] = farm_player.ToString();

                File.WriteAllLines(mylocation3, mystring3);
            }
        }
        void DataLoader()
        {
            //loads the data to the variables upon loading the game.
            string mylocation = Path.Combine(Helper.DirectoryPath, "Music_Expansion_Config");
            //string mylocation2 = mylocation + myname;
            string mylocation3 = mylocation + ".txt";
            if (!File.Exists(mylocation3)) //if not data.json exists, initialize the data variables to the ModConfig data. I.E. starting out.
            {
                delay_time_min = 10000;
                delay_time_max = 30000;
                silent_rain = false;
                seasonal_music = true;
                farm_player = true;
            }

            else
            {
                Monitor.Log("Music Expansion Config Loaded");
                //        Console.WriteLine("HEY THERE IM LOADING DATA");

                //loads the BuildEndurance_data upon loading the mod
                string[] readtext = File.ReadAllLines(mylocation3);
                delay_time_min = Convert.ToInt32(readtext[3]);
                delay_time_max = Convert.ToInt32(readtext[5]);  //these array locations refer to the lines in BuildEndurance_data.json
                silent_rain = Convert.ToBoolean(readtext[7]);
                seasonal_music = Convert.ToBoolean(readtext[9]);
                if (readtext[11] == "") farm_player = true;
                else farm_player = Convert.ToBoolean(readtext[11]);

            }
        }



    }//ends class1
}//ends namespace