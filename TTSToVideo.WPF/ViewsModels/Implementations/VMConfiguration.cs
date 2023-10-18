﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewModel.Implementations
{
    public class VMConfiguration : ObservableRecipient, IVMConfiguration
    {
        private MConfiguration model;
        public MConfiguration Model { get => model; set => SetProperty(ref model, value, true); }
        public AsyncRelayCommand SaveCommand { get; set; }
        public const string ConfigurationFile = "Configuration.json";

        public VMConfiguration()
        {
            SaveCommand = new AsyncRelayCommand(Save);
            this.Model = new MConfiguration();

        }

        private async Task Load()
        {
            await CreateConfIfNotExists();
            var json = await File.ReadAllTextAsync(ConfigurationFile);
            this.Model = JsonConvert.DeserializeObject<MConfiguration>(json);
        }

        private static async Task CreateConfIfNotExists()
        {
            if (!File.Exists(ConfigurationFile))
            {
                await File.WriteAllTextAsync(ConfigurationFile, "{}");
            }
        }

        public async Task Init()
        {
            await this.Load();

            if (string.IsNullOrEmpty(Model.ProjectBaseDirPrefix))
            {
                this.Model.ProjectBaseDirPrefix = "P-";
            }

            if (string.IsNullOrEmpty(this.Model.ProjectBaseDir))
            {
                this.Model.ProjectBaseDir = Directory.GetCurrentDirectory();
            }

            if (string.IsNullOrEmpty(this.Model.MusicDir))
            {
                this.Model.MusicDir = "";
            }
        }

        public async Task Save()
        {
            await CreateConfIfNotExists();
            string json = JsonConvert.SerializeObject(this.Model, Formatting.Indented);
            await File.WriteAllTextAsync("Configuration.json", json);
        }

    }
}
