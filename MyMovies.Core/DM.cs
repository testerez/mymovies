﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Web.Script.Serialization;
using JsonExSerializer;

namespace MyMovies.Core
{
    public class DM
    {
        private static DM instance;
        private readonly DataBase _data;
        private readonly String _dataFile = GetLocalFilePath("data.json");

        public static String GetLocalFilePath(params String[] path)
        {
            return Path.Combine(path.Prepend(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).ToArray());
        }

        public static DM Instance
        {
            get { return instance ?? (instance = new DM()); }
        }

        private DM()
        {
            if (File.Exists(_dataFile))
            {
                using (Stream s = File.OpenRead(@"C:\Users\Tom\projects\MyMovies\trunk\MyMovies.UI\bin\Debug\data.json"))
                {
                    var serializer = new Serializer(typeof (DataBase));
                    serializer.Config.MissingPropertyAction = MissingPropertyOptions.Ignore;
                    serializer.Config.IgnoredPropertyAction = SerializationContext.IgnoredPropertyOption.SetIfPossible;
                    _data = (DataBase)serializer.Deserialize(s);
                }
            }
            else
            {
                _data = new DataBase();
            }
        }

        public void Save()
        {
            lock (_data)
            {
                File.WriteAllText(_dataFile, GetJson(), Encoding.UTF8);
            }
        }

        public String GetJson()
        {
            lock (_data)
            {
                var serializer = new Serializer(typeof (DataBase));
                return serializer.Serialize(_data);
            }
        }

        public void AddMovie(Movie movie)
        {
            foreach (var f in movie.Files)
                RemoveFile(f);
            lock (_data)
            {
                var m = movie.ImdbId.IsNullOrEmpty() ? null : _data.Movies.FirstOrDefault(o => o.ImdbId == movie.ImdbId);
                if(m != null)
                {
                    movie.Files.AddRange(m.Files);
                    _data.Movies.Remove(m);
                }
                movie.Files.Sort();
                _data.Movies.Add(movie);
            }
        }

        public void RemoveFile(String file)
        {
            lock (_data)
            {
                _data.Unmatched.Remove(file);
                _data.Ignored.Remove(file);
                _data.Skipped.Remove(file);
                for(int i = 0; i < _data.Movies.Count; i++)
                {
                    var m = _data.Movies[i];
                    m.Files.Remove(file);
                    if (m.Files.Count < 1)
                    {
                        _data.Movies.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public void UnmatchMovie(String imdbId)
        {
            var movie = _data.Movies.FirstOrDefault(m => m.ImdbId == imdbId);
            if(movie == null)
                return;
            lock (_data)
            {
                _data.Movies.Remove(movie);
                foreach (var file in movie.Files)
                    _data.Unmatched.Add(file);
            }
        }

        public List<String> GetAllFiles()
        {
            lock(_data)
            {
                return _data.Movies.SelectMany(m => m.Files)
                    .Concat(_data.Ignored)
                    .Concat(_data.Skipped)
                    .ToList();
            }
        }

        public int CountMovies()
        {
            lock (_data)
            {
                return _data.Movies.Count;
            }
        }

        public bool PlayFile(string f)
        {
            if (!HasFile(f))
                return false;
            Process.Start(f);
            return true;
        }

        public bool HasFile(string f)
        {
            lock (_data)
            {
                return _data.Movies
                    .SelectMany(m => m.Files)
                    .Concat(_data.Unmatched)
                    .Concat(_data.Ignored)
                    .Concat(_data.Skipped)
                    .Contains(f);
            }
        }

        public void AddUnmatched(string path)
        {
            RemoveFile(path);
            lock (_data)
            {
                _data.Unmatched.Add(path);
            }
        }

        public void AddSkipped(string path)
        {
            RemoveFile(path);
            lock (_data)
            {
                _data.Skipped.Add(path);
            }
        }

        public void SetTags(IEnumerable<String> tags, String id)
        {
            lock (_data)
            {
                var movie = _data.Movies.FirstOrDefault(m => m.ImdbId == id);
                if(movie != null)
                    movie.Tags = tags.ToList();
            }
        }
    }

    public class DataBase
    {
        public List<Movie> Movies = new List<Movie>();
        public HashSet<String> Unmatched = new HashSet<String>();
        public HashSet<String> Ignored = new HashSet<String>();
        public HashSet<String> Skipped = new HashSet<String>();
        public HashSet<String> Tags = new HashSet<String>();
    }
}