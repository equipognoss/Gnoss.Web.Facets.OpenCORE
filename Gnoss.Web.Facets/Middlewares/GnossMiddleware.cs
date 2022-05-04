
using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ServicioCargaFacetasMVC.Middlewares
{
    public class GnossMiddleware
    {
        private IHostingEnvironment mEnv;
        private readonly RequestDelegate _next;
        private ConfigService mConfigService;

        public GnossMiddleware(RequestDelegate next, IHostingEnvironment env, ConfigService configService)
        {
            _next = next;
            mEnv = env;
            mConfigService = configService;
        }

        public async Task Invoke(HttpContext context, LoggingService loggingService, EntityContext entityContext, RedisCacheWrapper redisCacheWrapper)
        {
            Application_BeginRequest(entityContext, context, loggingService, redisCacheWrapper);
            await _next(context);
            Application_EndRequest(loggingService);
        }

        protected void Application_BeginRequest(EntityContext pEntityContext, HttpContext pHttpContextAccessor, LoggingService pLoggingService, RedisCacheWrapper pRedisCacheWrapper)
        {
            //ComprobarTrazaHabilitada(pEntityContext, pLoggingService, pRedisCacheWrapper, pHttpContextAccessor);
            pLoggingService.AgregarEntrada("TiemposMVC_Application_BeginRequest");


            pLoggingService.AgregarEntrada("TiemposMVC_Application_FinnRequest");
        }

        private void ComprobarTrazaHabilitada(EntityContext pEntityContext, LoggingService pLoggingService, RedisCacheWrapper pRedisCacheWrapper, HttpContext pHttpContext, IServicesUtilVirtuosoAndReplication pServicesUtilVirtuosoAndReplication, bool pForzarComprobacion = false)
        {
            if (pForzarComprobacion || LoggingService.HoraComprobacionCache == null || LoggingService.HoraComprobacionCache.AddSeconds(LoggingService.TiempoDuracionComprobacion) < DateTime.Now)
            {
                GnossCacheCL gnossCacheCL = new GnossCacheCL(pEntityContext, pLoggingService, pRedisCacheWrapper, mConfigService, pServicesUtilVirtuosoAndReplication);
                bool? trazaHabilitada = gnossCacheCL.ObtenerDeCache($"traza_5.0.0_{pHttpContext.Request.Host}") as bool?;

                if (trazaHabilitada.HasValue && trazaHabilitada.Value)
                {
                    LoggingService.TrazaHabilitada = true;
                    //LoggingService.TiempoMinPeticion = (int)tiempoMin;
                }
                else
                {
                    LoggingService.TrazaHabilitada = false;
                }

                LoggingService.HoraComprobacionCache = DateTime.Now;
            }
        }

        protected void Application_EndRequest(LoggingService pLoggingService)
        {
            try
            {

                pLoggingService.AgregarEntrada("TiemposMVC_Application_EndRequest");

                pLoggingService.GuardarTraza(ObtenerRutaTraza());
            }
            catch (Exception) { }
        }

        protected string ObtenerRutaTraza()
        {
            string ruta = Path.Combine(mEnv.ContentRootPath, "trazas");

            if (!Directory.Exists(ruta))
            {
                Directory.CreateDirectory(ruta);
            }

            ruta += Path.DirectorySeparatorChar + "traza_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";

            return ruta;
        }

    }

    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseGnossMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GnossMiddleware>();
        }
    }
}