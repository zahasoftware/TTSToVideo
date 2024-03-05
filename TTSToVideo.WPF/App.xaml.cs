using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.Processes;
using NetXP.TTS;
using NetXP.TTSs.ElevenLabs;
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
using TTSToVideo.WPF.Models;
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
        private IMainWindow? mw;

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

            services.AddOptions<TTSElevenlabsOptions>().Configure((o) =>
            {
                configuration.GetSection("TTSOptions").Bind(o);
                o.APIKey = configuration.GetSection("ElevenLabsToken").Value;
            });

            services.AddOptions<ImageGeneratorAIOptions>().Configure((o) =>
            {
                configuration.GetSection("ImageGeneratorAIOptions").Bind(o);
                o.Token = configuration.GetSection("LeonardoAIToken").Value;
            });

            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TTSToVideoModel, TTSToVideoModel>()
                        .ForMember(o => o.ProjectNameSelected, o => o.Ignore());
            });

            var mapper = mapperConfig.CreateMapper();

            services.AddSingleton<IMapper>(mapper);

            //Framework NetXP
            services.AddSingleton<IImageGeneratorAI, NetXP.ImageGeneratorAI.LeonardoAI.ImageGeneratorAILeonardoAI>();
            services.AddSingleton<ITTS, NetXP.TTSs.ElevenLabs.TTSEvenLabs>();
            services.AddSingleton<IIOTerminal, NetXP.Processes.Implementations.IOTerminal>();

            //MvvM
            services.AddSingleton<IVMTTSToVideoPage, VMTTSToVideoPage>();
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
#if DEBUG
            e.Handled = false;
#else
            e.Handled = true;
#endif
        }
    }
}
