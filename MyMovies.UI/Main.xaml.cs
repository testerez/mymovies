﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Monstro.Util;
using MyMovies.Core;
using Helper;
using Helper.IMDB;
using System.Windows.Threading;
using MyMovies.UI;

namespace MyMovies
{

    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        const string Password = "testpassword";
        DM DM = DM.Instance;

        public Window1()
        {
            InitializeComponent();
            WebServer.Start(8080, Password);
            UpdateTitle();
            Log.Listner = (cat, message, level) => {
                if(level < Log.Level.Info)
                    return;
                tbLog.Text += cat + ": " + message + "\n";
                tbLog.ScrollToEnd();                 
            };
            btWeb.Content = WebServer.GetHomeUrl();
            tbDirectories.Text = DM.Config.PathToScan.Join("\n");
        }

        protected override void OnClosed(EventArgs e)
        {
            WebServer.Stop();
            DM.Save();
            base.OnClosed(e);
        }

        /// <returns>success</returns>
        private bool UpdateSettings()
        {
            var pathsToScan = tbDirectories.Text.Split('\n')
                .Select(s => s.TrimOrNull())
                .Where(s => s != null)
                .ToList();

            foreach (var p in pathsToScan.Where(p => !Directory.Exists(p)))
            {
                MessageBox.Show(String.Format("Directory '{0}' does not exists", p));
                return false;
            }

            DM.Config.PathToScan = pathsToScan;
            DM.SaveConfig();
            return true;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (!UpdateSettings())
                return;
            var scanLog = new Log("scan");
            if(DM.Config.PathToScan.Count < 1)
            {
                scanLog.Warn("No directory to scan.");
                return;
            }
                
            var w = Reactor.Run(
                DM.Config.PathToScan,
                dirs => {
                    scanLog.Info("Scan started!");

                    //Search for new movies
                    foreach (String path in dirs.SelectMany(Scanner.GetFiles))
                    {
                        if(DM.Instance.HasFile(path))
                            continue;
                        scanLog.Info(path);

                        try
                        {
                            var m = Scanner.FetchMovie(path);
                            if(m == null)
                            {
                                scanLog.Info(">> ignored");
                                DM.AddSkipped(path);
                                continue;
                            }
                            scanLog.Info(">> found: " + m.Title + " " + m.Year);
                            DM.AddMovie(m);

                            Dispatcher.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(delegate
                            {
                                UpdateTitle();
                                return null;
                            }), null);
                        }
                        catch(Exception ex)
                        {
                            if (ex is Scanner.NoMatchFoundException)
                            {
                                DM.AddUnmatched(path);
                                scanLog.Warn(">> no match found");
                                continue;
                            }
                                
                            scanLog.Error(ex.Message);
                        }
                    }

                    //Dlete missing movies
                    int cmpt = 0;
                    foreach (var path in DM.Instance.GetAllFiles().Where(p => !File.Exists(p)))
                    {
                        DM.RemoveFile(path);
                        cmpt++;
                    }
                    scanLog.Info(cmpt + " missing files deleted");

                    return "";
                },
                null, null, (s, ex) => scanLog.Info("Scan finished"));
        }

        void UpdateTitle()
        {
            Title = "My movies - " + DM.CountMovies();
        }

        private void btWeb_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebServer.GetHomeUrl());
        }
    }
}
