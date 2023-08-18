﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.Processes;
using NetXP.TTS;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using TTSToVideo.WPF;
using TTSToVideo.WPF.Pages;
using TTSToVideo.WPF.ViewModel;
using TTSToVideo.WPF.ViewModel.Implementations;

namespace TTSToVideo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IMainWindow mw;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            //Net Core
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<App>()
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddHttpClient();

            services.AddOptions<TTSOptions>().Configure((o) =>
            {
                configuration.GetSection("TTSOptions").Bind(o);
            });

            services.AddOptions<ImageGeneratorAIOptions>().Configure((o) =>
            {
                configuration.GetSection("ImageGeneratorAIOptions").Bind(o);
                o.Token = configuration.GetSection("LeonardoAIToken").Value;
            });

            //Framework NetXP
            services.AddSingleton<IImageGeneratorAI, NetXP.ImageGeneratorAI.LeonardoAI.ImageGeneratorAILeonardoAI>();
            services.AddSingleton<ITTS, NetXP.TTS.OpenTTS.TTSOpenTTS>();
            services.AddSingleton<IIOTerminal, NetXP.Processes.Implementations.IOTerminal>();

            //DAO
            services.AddSingleton<ITTS, NetXP.TTS.OpenTTS.TTSOpenTTS>();

            //MvvM
            services.AddSingleton<IVMMainPage, VMMainPage>();
            services.AddSingleton<IVMMainWindow, VMMainWindow>();
            services.AddSingleton<IVMConfiguration, VMConfiguration>();

            //Views
            services.AddSingleton<IMainWindow, MainWindow>();
            services.AddSingleton<IMainPage, TTSToVideoPage>();
            services.AddSingleton<IPage<ConfigurationPage>, ConfigurationPage>();

            var serviceProvider = services.BuildServiceProvider();

            this.mw = serviceProvider.GetRequiredService<IMainWindow>();
            mw.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is CustomApplicationException)
            {
                this.mw.ViewModel.Message = e.Exception.Message;
            }
            else
            {
                MessageBox.Show($"{e.Exception.Message} , See detail in Exception.txt");
                File.WriteAllText("Exception.txt", e.Exception.ToString());
            }
            e.Handled = true;
        }
    }
}
