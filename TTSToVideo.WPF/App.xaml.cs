using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetXP;
using NetXP.Exceptions;
using NetXP.IAs.Chat;
using NetXP.IAs.Chats.Ollama;
using NetXP.IAs.ImageGeneratorAI;
using NetXP.ImageGeneratorAI.LeonardoAI;
using NetXP.Processes;
using NetXP.Translators.AzureTranslator;
using NetXP.Tts;
using NetXP.Tts.ElevenLabs;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using TTSToVideo.Business;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.Helpers.Implementations;
using TTSToVideo.WPF;
using TTSToVideo.WPF.Helpers.Implementations;
using TTSToVideo.WPF.Models;
using TTSToVideo.WPF.Pages;
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private MainWindow? mw;

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


            services.AddOptions<TtsElevenlabsOptions>().Configure((o) =>
            {
                configuration.GetSection("ElevenlabsOptions").Bind(o);
                o.APIKey = configuration.GetSection("ElevenLabsToken").Value!;
            });
            services.AddHttpClient<TtsEvenLabs>();

            services.AddOptions<ImageGeneratorAIOptions>().Configure((o) =>
            {
                configuration.GetSection("ImageGeneratorAIOptions").Bind(o);
                o.Token = configuration.GetSection("LeonardoAIToken").Value;
            });
            services.AddHttpClient<ImageGeneratorAILeonardoAI>();

            services.AddOptions<AIChatConfig>().Configure((o) => configuration.GetSection("AIChatConfig").Bind(o));
            services.AddHttpClient<OllamaChatService>();

            services.AddOptions<AzureTranslatorOptions>().Configure((o) => configuration.GetSection("AzureTranslator").Bind(o));
            services.AddSingleton<ITranslator, AzureTranslatorImplementation>();

            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TtsToVideoModel, TtsToVideoModel>();
                cfg.CreateMap<StatementImageModel, StatementImage>();
                cfg.CreateMap<StatementImage, StatementImageModel>();
            });

            mapperConfig.AssertConfigurationIsValid();

            var mapper = mapperConfig.CreateMapper();

            services.AddSingleton(mapper);

            //Framework NetXP
            services.AddSingleton<IImageGeneratorAI, NetXP.ImageGeneratorAI.LeonardoAI.ImageGeneratorAILeonardoAI>();
            services.AddSingleton<ITts, TtsEvenLabs>();
            services.AddSingleton<IAIChatService, OllamaChatService>();
            services.AddSingleton<IIOTerminal, NetXP.Processes.Implementations.IOTerminal>();

            services.AddSingleton<IProgressBar, ProgressBar>();
            services.AddSingleton<IMessage, Messages>();
            services.AddSingleton<IVideoGeneratorFactory, LeonardoVideoGeneratorFactory>();

            //Business
            services.AddSingleton<ITTSToVideoBusiness, Business.Implementations.TTSToVideoBusiness>();

            //MvvM
            services.AddSingleton<TTSToVideoViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<ConfigurationViewModel>();
            services.AddSingleton<FontStyleViewModel>();
            services.AddSingleton<NewProjectViewModel>();
            services.AddSingleton<NewCategoryViewModel>();

            //Views
            services.AddSingleton<MainWindow>();
            services.AddTransient<FontStyleWindowsView>();
            services.AddSingleton<TTSToVideoPage>();
            services.AddSingleton<ConfigurationPage>();
            services.AddTransient<NewProjectWindow>();
            services.AddTransient<NewCategoryView>();
            services.AddTransient<CategoryConfigurationView>();


            var serviceProvider = services.BuildServiceProvider();

            this.mw = serviceProvider.GetRequiredService<MainWindow>();
            mw.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            switch (e.Exception)
            {
                case OperationCanceledException oce when oce.CancellationToken.CanBeCanceled 
                                                       && oce.CancellationToken.IsCancellationRequested:
                    // Normal cancellation – silently swallow or log minimally.
                    // Example:
                    // Debug.WriteLine("Operation canceled.");
                    e.Handled = true;
                    return;

                case CustomApplicationException cae:
                    this.mw!.ViewModel.Message = cae.Message;
                    MessageBox.Show($"{e.Exception.Message}");

                    e.Handled = true;
                    return;
            }
            MessageBox.Show($"{e.Exception.Message} , See detail in Exception.txt");
            File.WriteAllText("Exception.txt", e.Exception.ToString());
            e.Handled = false;
        }
    }
}
