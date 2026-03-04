using System;
using System.Threading.Tasks;
using App1.Data.DataSources;
using App1.Data.Interfaces;
using App1.Data.Repositories;
using App1.Domain.Interfaces;
using App1.Domain.UseCases;
using App1.Infrastructure;
using App1.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace App1;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// InstanceId được lấy từ InstanceSlotManager: persist theo slot, mở lại app vẫn thấy thiết bị đã mượn;
    /// nhiều instance mỗi cái một slot riêng nên không thấy thiết bị của instance khác.
    /// </summary>
    public static string InstanceId { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        InstanceId = InstanceSlotManager.GetOrCreateInstanceId();

        var services = new ServiceCollection();

        // Data Layer
        services.AddSingleton<ISqliteDataSource, SqliteDataSource>();
        services.AddSingleton<IDeviceModelRepository, DeviceModelRepository>();
        services.AddSingleton<IDeviceRepository, DeviceRepository>();

        // Domain Layer - Use Cases
        services.AddTransient<GetDeviceModelsUseCase>();
        services.AddTransient<BorrowDeviceUseCase>();
        services.AddTransient<ReturnDeviceUseCase>();
        services.AddTransient<GetDevicesUseCase>();
        services.AddTransient<GetCategoriesUseCase>();

        // Infrastructure
        services.AddSingleton(sp => new SyncService(InstanceId));

        // Presentation
        services.AddTransient<RequestDeviceViewModel>();
        services.AddTransient<MyDeviceViewModel>();

        Services = services.BuildServiceProvider();

        var ds = Services.GetRequiredService<ISqliteDataSource>();
        await Task.Run(() => ds.InitializeAsync());

        var sync = Services.GetRequiredService<SyncService>();
        sync.StartListening();

        _window = new MainWindow();
        _window.Activate();
    }
}
