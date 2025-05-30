﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TombLib.IO;
using TombLib.LevelData;
using TombLib.Utils;
using TombLib.Wad.Catalog;
using TombLib.Wad.Tr4Wad;

namespace TombLib.Wad
{
    public class SamplePathInfo
    {
        public string Name { get; set; }
        public string FullPath { get; set; } = null;
        public bool Found { get { return (!string.IsNullOrEmpty(FullPath)) && File.Exists(FullPath); } }
    }

    public class WadSounds
    {
        public List<WadSoundInfo> SoundInfos { get; set; }

        public WadSounds()
        {
            SoundInfos = new List<WadSoundInfo>();
        }

        public WadSounds(IEnumerable<WadSoundInfo> soundInfos)
        {
            SoundInfos = soundInfos.Where(s => s != null).ToList();
        }

        public static WadSounds ReadFromFile(string filename)
        {
            string extension = Path.GetExtension(filename).ToLower();
            if (extension == ".xml")
                return ReadFromXml(filename);
            else if (extension == ".txt")
                return ReadFromTxt(filename);
            else if (extension == ".sfx" && SFXIsValid(filename))
                return ReadFromSfx(filename);
            throw new ArgumentException("Invalid file format");
        }

        private static WadSounds ReadFromXml(string filename)
        {
            WadSounds sounds = XmlUtils.ReadXmlFile<WadSounds>(filename);

            foreach (var soundInfo in sounds.SoundInfos)
                soundInfo.SoundCatalog = filename;

            return sounds;
        }

        public static bool SaveToXml(string filename, WadSounds sounds)
        {
            XmlUtils.WriteXmlFile(filename, sounds);
            return true;
        }

        public static List<string> GetFormattedList(Level level, TRVersion.Game version)
        {
            var result = new List<string>();

            var defaultSoundList = TrCatalog.GetAllSounds(version);
            var soundCatalogPresent = level != null && level.Settings.GlobalSoundMap.Count > 0;

            var maxKnownSound = -1;

            foreach (var sound in defaultSoundList)
                if (sound.Key > maxKnownSound) maxKnownSound = (int)sound.Key;

            if (soundCatalogPresent)
                foreach (var sound in level.Settings.GlobalSoundMap)
                    if (sound.Id > maxKnownSound) maxKnownSound = sound.Id;

            for (int i = 0; i <= maxKnownSound; i++)
            {
                var lbl = i.ToString().PadLeft(4, '0') + ": ";

                if (soundCatalogPresent && level.Settings.GlobalSoundMap.Any(item => item.Id == i))
                    lbl += level.Settings.GlobalSoundMap.First(item => item.Id == i).Name;
                else if (defaultSoundList.Any(item => item.Key == i))
                    lbl += defaultSoundList.First(item => item.Key == i).Value;
                else
                    lbl += "Unknown sound";

                result.Add(lbl);
            }

            return result;
        }

        public WadSoundInfo TryGetSoundInfo(int id)
        {
            foreach (var soundInfo in SoundInfos)
                if (soundInfo.Id == id)
                    return soundInfo;
            return null;
        }

        public static string TryGetSamplePath(List<string> wadSoundPaths, string name)
        {
            // Search the sample in all registered paths, in descending order
            foreach (var path in wadSoundPaths)
            {
                var samplePath = Path.Combine(path, name);
                if (File.Exists(samplePath))
                    return samplePath;
            }

            return null;
        }

        private static WadSounds ReadFromSfx(string filename)
        {
            var samples = new List<string>();

            // Read version
            int version = 129;
            using (var readerVersion = new BinaryReaderEx(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                version = readerVersion.ReadInt32();

            // Read samples
            var samPath = Path.GetDirectoryName(filename) + "\\" + Path.GetFileNameWithoutExtension(filename) + ".sam";
            using (var readerSounds = new StreamReader(new FileStream(samPath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                while (!readerSounds.EndOfStream)
                    samples.Add(readerSounds.ReadLine());

            // Read sounds
            int soundMapSize = 0;
            short[] soundMap; 
            var wadSoundInfos = new List<wad_sound_info>();
            var soundInfos = new List<WadSoundInfo>();

            var sfxPath = Path.GetDirectoryName(filename) + "\\" + Path.GetFileNameWithoutExtension(filename) + ".sfx";
            using (var readerSfx = new BinaryReaderEx(new FileStream(sfxPath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                // Try to guess the WAD version
                readerSfx.BaseStream.Seek(740, SeekOrigin.Begin);
                short first = readerSfx.ReadInt16();
                short second = readerSfx.ReadInt16();
                version = ((first == -1 || second == (first + 1)) ? 130 : 129);
                readerSfx.BaseStream.Seek(0, SeekOrigin.Begin);

                soundMapSize = (version == 130 ? 2048 : 370);
                soundMap = new short[soundMapSize];
                for (var i = 0; i < soundMapSize; i++)
                {
                    soundMap[i] = readerSfx.ReadInt16();
                }

                var numSounds = readerSfx.ReadUInt32();

                for (var i = 0; i < numSounds; i++)
                {
                    var info = new wad_sound_info();
                    info.Sample = readerSfx.ReadUInt16();
                    info.Volume = readerSfx.ReadByte();
                    info.Range = readerSfx.ReadByte();
                    info.Chance = readerSfx.ReadByte();
                    info.Pitch = readerSfx.ReadByte();
                    info.Characteristics = readerSfx.ReadUInt16();
                    wadSoundInfos.Add(info);
                }
            }

            // Convert old data to new format
            for (int i = 0; i < soundMapSize; i++)
            {
                // Check if sound is defined at all
                if (soundMap[i] == -1 || soundMap[i] >= wadSoundInfos.Count)
                    continue;

                // Fill the new sound info
                var oldInfo = wadSoundInfos[soundMap[i]];
                var newInfo = new WadSoundInfo(i);
                newInfo.Name = TrCatalog.GetOriginalSoundName(TRVersion.Game.TR4, (uint)i);
                newInfo.Volume = (int)Math.Round(oldInfo.Volume * 100.0f / 255.0f);
                newInfo.RangeInSectors = oldInfo.Range;
                newInfo.Chance = (int)Math.Round(oldInfo.Chance * 100.0f / 255.0f);
                if (newInfo.Chance == 0) newInfo.Chance = 100; // Convert legacy chance value
                newInfo.PitchFactor = (int)Math.Round((oldInfo.Pitch > 127 ? oldInfo.Pitch - 256 : oldInfo.Pitch) * 100.0f / 128.0f);
                newInfo.RandomizePitch = ((oldInfo.Characteristics & 0x2000) != 0);
                newInfo.RandomizeVolume = ((oldInfo.Characteristics & 0x4000) != 0);
                newInfo.DisablePanning = ((oldInfo.Characteristics & 0x1000) != 0);
                newInfo.LoopBehaviour = (WadSoundLoopBehaviour)(oldInfo.Characteristics & 0x03);
                newInfo.SoundCatalog = sfxPath;

                // Read all samples linked to this sound info (for example footstep has 4 samples)
                int numSamplesInGroup = (oldInfo.Characteristics & 0x00fc) >> 2;
                for (int j = oldInfo.Sample; j < oldInfo.Sample + numSamplesInGroup; j++)
                {
                    newInfo.Samples.Add(new WadSample(samples[j]));
                }
                soundInfos.Add(newInfo);
            }

            var sounds = new WadSounds(soundInfos);

            return sounds;
        }

        private static bool SFXIsValid(string path)
        {
            if (System.IO.Path.GetExtension(path).Equals(".sfx", StringComparison.InvariantCultureIgnoreCase))
            {
                using (var reader = new BinaryReaderEx(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    var riffSignature = reader.ReadUInt32();
                    if (riffSignature == 0x46464952)
                        return false;
                    else
                        return true;
                }
            }
            else
                return true;
        }

        private static WadSounds ReadFromTxt(string filename)
        {
            var sounds = new WadSounds();

            try
            {
                using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using (var reader = new StreamReader(fileStream))
                    {
                        ushort soundId = 0;
                        while (!reader.EndOfStream)
                        {
                            var s = reader.ReadLine().Trim();
                            var sound = new WadSoundInfo(soundId);
                            
                            // Get the name (ends with :)
                            int endOfSoundName = s.IndexOf(':');
                            if (endOfSoundName == -1)
                            {
                                soundId++;
                                continue;
                            }

                            sound.Name = s.Substring(0, endOfSoundName);
                            sound.SoundCatalog = filename;

                            // Get everything else and remove empty tokens
                            var tokens = s.Substring(endOfSoundName + 1).Split(' ', '\t').Where(token => !string.IsNullOrEmpty(token)).ToList();
                            if (tokens.Count <= 1)
                            {
                                soundId++;
                                continue;
                            }

                            // Trim comments
                            int commentTokenIndex = tokens.FindIndex(t => t.StartsWith(";"));
                            if (commentTokenIndex != -1)
                                tokens.RemoveRange(commentTokenIndex, tokens.Count - commentTokenIndex);

                            var anyTokenFound = false;
                            for (int i = 0; i < tokens.Count; i++)
                            {
                                var   token = tokens[i];
                                short temp  = 0;

                                if (token.StartsWith("PIT", StringComparison.InvariantCultureIgnoreCase) && short.TryParse(token.Substring(3), out temp))
                                {
                                    sound.PitchFactor = temp;
                                    anyTokenFound = true;
                                }
                                else if (token.StartsWith("RAD", StringComparison.InvariantCultureIgnoreCase) && short.TryParse(token.Substring(3), out temp))
                                {
                                    sound.RangeInSectors = temp;
                                    anyTokenFound = true;
                                }
                                else if (token.StartsWith("VOL", StringComparison.InvariantCultureIgnoreCase) && short.TryParse(token.Substring(3), out temp))
                                {
                                    sound.Volume = temp;
                                    anyTokenFound = true;
                                }
                                else if (token.StartsWith("CH", StringComparison.InvariantCultureIgnoreCase) && short.TryParse(token.Substring(2), out temp))
                                {
                                    sound.Chance = temp;
                                    anyTokenFound = true;
                                }
                                else if (token.Equals("P", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sound.RandomizePitch = true;
                                    anyTokenFound = true;
                                }
                                else if (token.Equals("V", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sound.RandomizeVolume = true;
                                    anyTokenFound = true;
                                }
                                else if (token.Equals("N", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sound.DisablePanning = true;
                                    anyTokenFound = true;
                                }
                                else if (token.Equals("L", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sound.LoopBehaviour = WadSoundLoopBehaviour.Looped;
                                    anyTokenFound = true;
                                }
                                else if (token.Equals("R", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sound.LoopBehaviour = WadSoundLoopBehaviour.OneShotRewound;
                                    anyTokenFound = true;
                                }
                                else if (token.Equals("W", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    sound.LoopBehaviour = WadSoundLoopBehaviour.OneShotWait;
                                    anyTokenFound = true;
                                }
                                else if (token.StartsWith("#g"))
                                {
                                    sound.Global = true;
                                    anyTokenFound = true;
                                }
                                else if (!token.StartsWith("#") && !anyTokenFound)
                                    sound.Samples.Add(new WadSample(token + ".wav"));

                                if (token.StartsWith("#"))
                                    sound.Indexed = true;
                            }

                            sounds.SoundInfos.Add(sound);
                            soundId++;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return sounds;
        }

		public enum SoundtrackFormat
		{
			Wav,
			Mp3,
			Ogg
		}

        public static IReadOnlyList<FileFormat> FileExtensions { get; } = new List<FileFormat>()
        {
            new FileFormat("TRLE Txt format", "txt"),
            new FileFormat("Tomb Editor Xml format", "xml"),
            new FileFormat("Compiled TRLE Sfx/Sam", "sfx")
        };

        public static readonly string AudioFolder = "Audio";
    }
}
