using CommunityToolkit.Mvvm.ComponentModel;
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

namespace TTSToVideo.WPF.ViewsModels
{
    public class ConfigurationViewModel : ObservableRecipient
    {
        private ConfigurationModel model;
        public ConfigurationModel Model { get => model; set => SetProperty(ref model, value, true); }
        public AsyncRelayCommand SaveCommand { get; set; }
        public const string ConfigurationFile = "Configuration.json";

        public ConfigurationViewModel()
        {
            SaveCommand = new AsyncRelayCommand(Save);
            Model = new ConfigurationModel();
        }

        private async Task Load()
        {
            await CreateConfIfNotExists();
            var json = await File.ReadAllTextAsync(ConfigurationFile);
            Model = JsonConvert.DeserializeObject<ConfigurationModel>(json);
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
            await Load();

            if (string.IsNullOrEmpty(Model.ProjectBaseDir))
            {
                Model.ProjectBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "Projects");
            }

            if (string.IsNullOrEmpty(Model.MusicDir))
            {
                Model.MusicDir = "";
            }
        }

        public async Task Save()
        {
            await CreateConfIfNotExists();
            string json = JsonConvert.SerializeObject(Model, Formatting.Indented);
            await File.WriteAllTextAsync("Configuration.json", json);
        }

    }
}
