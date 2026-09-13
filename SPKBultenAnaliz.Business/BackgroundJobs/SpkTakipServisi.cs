using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SPKBultenAnaliz.Core.Interfaces;

namespace SPKBultenAnaliz.Business.BackgroundJobs
{
    /// <summary>
    /// Uygulama ayaktayken belirli periyotlarla SPK sitesini kontrol eden
    /// arka plan servisi (BackgroundService). Use Case 1.2'nin çalışma zamanı motoru.
    ///
    /// ÖĞRENME NOTU: BackgroundService, .NET'in ASP.NET Core dışında (veya
    /// birlikte) çalışan uzun ömürlü görevler için sağladığı yerleşik bir
    /// alt yapıdır. IHostedService'i uygular ve ExecuteAsync metodunu
    /// uygulama başlarken bir kez tetikler; siz de içinde bir döngü kurarsınız.
    /// </summary>
    public class SpkTakipServisi : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SpkTakipServisi> _logger;
        private static readonly TimeSpan TaramaPeriyodu = TimeSpan.FromHours(6);

        public SpkTakipServisi(IServiceProvider serviceProvider, ILogger<SpkTakipServisi> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SPK Takip Servisi başlatıldı. Tarama periyodu: {Periyot}", TaramaPeriyodu);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // BackgroundService Singleton ömründedir; ama IBultenService (ve onun
                    // bağımlılıkları) Scoped olabilir. Bu yüzden her taramada yeni bir
                    // "scope" oluşturup Scoped servisleri güvenle enjekte ederiz.
                    using var scope = _serviceProvider.CreateScope();
                    var bultenService = scope.ServiceProvider.GetRequiredService<IBultenService>();

                    var islenenSayisi = await bultenService.YeniBultenleriIsleAsync(stoppingToken);

                    if (islenenSayisi > 0)
                        _logger.LogInformation("{Sayi} yeni bülten işlendi.", islenenSayisi);
                    else
                        _logger.LogInformation("Yeni bülten bulunamadı.");
                }
                catch (Exception ex)
                {
                    // Arka plan servisi hiçbir koşulda tamamen çökmemeli;
                    // hata loglanır, bir sonraki periyotta tekrar denenir.
                    _logger.LogError(ex, "SPK tarama döngüsünde beklenmeyen hata oluştu.");
                }

                await Task.Delay(TaramaPeriyodu, stoppingToken);
            }
        }
    }
}
