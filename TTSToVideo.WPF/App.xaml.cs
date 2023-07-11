using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetXP.ImageGeneratorAI;
using NetXP.TTS;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using TTSToVideo.WPF;
using TTSToVideo.WPF.ViewModel;
using TTSToVideo.WPF.ViewModel.Implementations;

namespace TTSToVideo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<App>()
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddHttpClient();

            //Windows
            services.AddSingleton<IMainWindow, MainWindow>();
            services.AddSingleton<IMainPage, MainPage>();

            //Framework NetXP
            services.AddSingleton<IImageGeneratorAI, NetXP.ImageGeneratorAI.LeonardoAI.ImageGeneratorAILeonardoAI>();
            services.AddSingleton<ITTS, NetXP.TTS.OpenTTS.TTSOpenTTS>();

            //DAO
            services.AddSingleton<ITTS, NetXP.TTS.OpenTTS.TTSOpenTTS>();

            //MvvM
            services.AddSingleton<IVMMainPage, VMMainPage>();
            services.AddSingleton<IVMMainWindow, VMMainWindow>();

            services.AddOptions<TTSOptions>().Configure((o) => { configuration.GetSection("TTSOptions").Bind(o); });
            services.AddOptions<ImageGeneratorAIOptions>().Configure((o) => 
            { 
                configuration.GetSection("ImageGeneratorAIOptions").Bind(o);
                o.Token = configuration.GetSection("LeonardoAIToken").Value;
            });

            var serviceProvider = services.BuildServiceProvider();

            var mainWindow = serviceProvider.GetRequiredService<IMainWindow>();
            mainWindow.Show();
        }
    }
}
