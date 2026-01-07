using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Facetado;
using Es.Riam.Gnoss.AD.ParametroAplicacion;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.RelatedVirtuoso;
using Es.Riam.Gnoss.Recursos;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Util.Seguridad;
using Es.Riam.Gnoss.UtilServiciosWeb;
using Es.Riam.Interfaces.InterfacesOpen;
using Es.Riam.Open;
using Es.Riam.OpenReplication;
using Es.Riam.Util;
using Gnoss.Web.Services.VirtualPathProvider;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using ServicioCargaFacetasMVC.Middlewares;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gnoss.Web.Facetas
{
    public class Startup
    {
        public Startup(IConfiguration configuration, Microsoft.AspNetCore.Hosting.IHostingEnvironment environment)
        {
            Configuration = configuration;
            mEnvironment = environment;
        }

        public IConfiguration Configuration { get; }
        public Microsoft.AspNetCore.Hosting.IHostingEnvironment mEnvironment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
			ILoggerFactory loggerFactory =
			LoggerFactory.Create(builder =>
			{
				builder.AddConfiguration(Configuration.GetSection("Logging"));
				builder.AddSimpleConsole(options =>
				{
					options.IncludeScopes = true;
					options.SingleLine = true;
					options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
					options.UseUtcTimestamp = true;
				});
			});

			services.AddSingleton(loggerFactory);
			bool cargado = false;

            services.AddCors(options =>
            {
                options.AddPolicy(name: "_myAllowSpecificOrigins",
                                  builder =>
                                  {
                                      builder.SetIsOriginAllowed(UtilServicios.ComprobarDominioPermitidoCORS);
                                      builder.AllowAnyHeader();
                                      builder.AllowAnyMethod();
                                      builder.AllowCredentials();
                                  });
            });

            services.AddControllers();
            services.AddHttpContextAccessor();
            services.AddScoped(typeof(UtilTelemetry));
            services.AddScoped(typeof(Usuario));
            services.AddScoped(typeof(UtilPeticion));
            services.AddScoped(typeof(Conexion));
            services.AddScoped(typeof(UtilGeneral));
            services.AddScoped(typeof(LoggingService));
            services.AddScoped(typeof(RedisCacheWrapper));
            services.AddScoped(typeof(Configuracion));
            services.AddScoped(typeof(GnossCache)); 
            services.AddScoped(typeof(VirtuosoAD));
            services.AddScoped(typeof(UtilServicios));
            services.AddScoped(typeof(BDVirtualPath));
            services.AddScoped<IUtilServicioIntegracionContinua, UtilServicioIntegracionContinuaOpen>();
            services.AddScoped<IServicesUtilVirtuosoAndReplication, ServicesVirtuosoAndBidirectionalReplicationOpen>();
            services.AddScoped(typeof(RelatedVirtuosoCL));
            services.AddScoped<IAvailableServices, AvailableServicesOpen>();
            string bdType = "";
            IDictionary environmentVariables = Environment.GetEnvironmentVariables();
            if (environmentVariables.Contains("connectionType"))
            {
                bdType = environmentVariables["connectionType"] as string;
            }
            else
            {
                bdType = Configuration.GetConnectionString("connectionType");
            }
            if (bdType.Equals("2") || bdType.Equals("1"))
            {
                services.AddScoped(typeof(DbContextOptions<EntityContext>));
                services.AddScoped(typeof(DbContextOptions<EntityContextBASE>));
            }
            services.AddSingleton(typeof(ConfigService));
			services.AddSession(options => {
				options.IdleTimeout = TimeSpan.FromMinutes(60); // Tiempo de expiración   
																//options.Cookie.Name = "AppTest";
																//options.Cookie.HttpOnly = true; // correct initialization

			});
			services.AddMvc();

            if (bdType.Equals("0"))
            {
                services.AddDbContext<EntityContext>();
                services.AddDbContext<EntityContextBASE>();
            }
            else if (bdType.Equals("1"))
            {
                services.AddDbContext<EntityContext, EntityContextOracle>();
                services.AddDbContext<EntityContextBASE, EntityContextBASEOracle>();
            }
            else if (bdType.Equals("2"))
            {
                services.AddDbContext<EntityContext, EntityContextPostgres>();
                services.AddDbContext<EntityContextBASE, EntityContextBASEPostgres>();
            }
            var sp = services.BuildServiceProvider();
            var loggingService = sp.GetService<LoggingService>();
            var virtualProvider = sp.GetService<BDVirtualPath>();
            while (!cargado)
            {
                try
                {
                    services.AddRazorPages().AddRazorRuntimeCompilation();
                    services.AddControllersWithViews().AddRazorRuntimeCompilation();
                    services.Configure<MvcRazorRuntimeCompilationOptions>(opts =>
                    {

                        opts.FileProviders.Add(
                            new BDFileProvider(loggingService, virtualProvider));
                    });
                    cargado = true;
                }
                catch (Exception)
                {
                    cargado = false;
                }
            }

            // Resolve the services from the service provider
            var configService = sp.GetService<ConfigService>();
            configService.ObtenerProcesarStringGrafo();

			var redisCacheWrapper = sp.GetService<RedisCacheWrapper>();
			var servicesUtilVirtuosoAndReplication = sp.GetService<IServicesUtilVirtuosoAndReplication>();
			string configLogStash = configService.ObtenerLogStashConnection();
            if (!string.IsNullOrEmpty(configLogStash))
            {
                LoggingService.InicializarLogstash(configLogStash);
            }
            
            var entity = sp.GetService<EntityContext>();
            LoggingService.RUTA_DIRECTORIO_ERROR = Path.Combine(mEnvironment.ContentRootPath, "logs");

            BaseCL.UsarCacheLocal = UsoCacheLocal.Siempre;

            string rutaVersionCacheLocal = $"{AppDomain.CurrentDomain.SetupInformation.ApplicationBase}/config/versionCacheLocal/";
            if (!Directory.Exists(rutaVersionCacheLocal)) { Directory.CreateDirectory(rutaVersionCacheLocal); }

            EstablecerDominioCache(entity);

            UtilServicios.CargarIdiomasPlataforma(entity, loggingService, configService, servicesUtilVirtuosoAndReplication, redisCacheWrapper, loggerFactory);

            GnossUrlsSemanticas.IdiomaPrincipalDominio = UtilServicios.IDIOMA_PRINCIPAL_DOMINIO;

            CargarTextosPersonalizadosDominio(entity, loggingService, configService, redisCacheWrapper, loggerFactory);

            ConfigurarApplicationInsights(configService);

			UtilServicios.CargarDominiosPermitidosCORS(entity);

			EstablecerPropiedadesFacetadoAD(entity);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gnoss.Web.Facetas", Version = "v1" });
            });
        }

		private void EstablecerPropiedadesFacetadoAD(EntityContext entity)
		{
            FacetadoAD.EsPeticionFacetas = true;
            var priv = entity.ParametroAplicacion.FirstOrDefault(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.UsarPrivacidadEnFacetasYResultados));

            if (priv != null && priv.Valor.Equals("false"))
            {
                FacetadoAD.UsarPrivacidadEnFacetasYResultados = false;
            }

            var usarRegex = entity.ParametroAplicacion.FirstOrDefault(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.UsarRegexParaBusquedaPorTextoLibre));
            if (usarRegex != null && usarRegex.Valor.Equals("true"))
            {
                FacetadoAD.UsarRegexParaBusquedaPorTextoLibre = true;
            }
        }

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "Gnoss.Web.Facetas v1"));
            app.UseRouting();
            app.UseCors();
            app.UseAuthorization();
			app.UseSession();
			app.UseGnossMiddleware();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        /// <summary>
        /// Establece el dominio de la cache.
        /// </summary>
        private void EstablecerDominioCache(EntityContext entity)
        {
            string dominio = entity.ParametroAplicacion.Where(parametroApp => parametroApp.Parametro.Equals("UrlIntragnoss")).FirstOrDefault().Valor;

            dominio = dominio.Replace("http://", "").Replace("https://", "").Replace("www.", "");

            if (dominio[dominio.Length - 1] == '/')
            {
                dominio = dominio.Substring(0, dominio.Length - 1);
            }

            BaseCL.DominioEstatico = dominio;
        }

        private void CargarTextosPersonalizadosDominio(EntityContext context, LoggingService loggingService, ConfigService configService, RedisCacheWrapper redisCacheWrapper, ILoggerFactory mLoggerFactory)
        {
            string dominio = "";//mEnvironment.ApplicationName;
            Guid personalizacionEcosistemaID = Guid.Empty;
            List<ParametroAplicacion> parametrosAplicacionPers = context.ParametroAplicacion.Where(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.PersonalizacionEcosistemaID.ToString())).ToList();
            if (parametrosAplicacionPers.Count > 0)
            {
                personalizacionEcosistemaID = new Guid(parametrosAplicacionPers[0].Valor.ToString());
            }
            UtilIdiomas utilIdiomas = new UtilIdiomas(string.Empty, loggingService, context, configService, redisCacheWrapper, mLoggerFactory.CreateLogger<UtilIdiomas>(), mLoggerFactory);
            utilIdiomas.CargarTextosPersonalizadosDominio(dominio, personalizacionEcosistemaID);
        }

        private void ConfigurarApplicationInsights(ConfigService configService)
        {
            string valor = configService.ObtenerImplementationKeyFacetas();

            if (!string.IsNullOrEmpty(valor))
            {
                Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.Active.InstrumentationKey = valor.ToLower();
            }

            if (UtilTelemetry.EstaConfiguradaTelemetria)
            {
                //Configuración de los logs

                string ubicacionLogs = configService.ObtenerUbicacionLogsFacetas();

                int valorInt = 0;
                if (int.TryParse(ubicacionLogs, out valorInt))
                {
                    if (Enum.IsDefined(typeof(UtilTelemetry.UbicacionLogsYTrazas), valorInt))
                    {
                        LoggingService.UBICACIONLOGS = (UtilTelemetry.UbicacionLogsYTrazas)valorInt;
                    }
                }


                //Configuración de las trazas

                string ubicacionTrazas = configService.ObtenerUbicacionTrazasFacetas();

                int valorInt2 = 0;
                if (int.TryParse(ubicacionTrazas, out valorInt2))
                {
                    if (Enum.IsDefined(typeof(UtilTelemetry.UbicacionLogsYTrazas), valorInt2))
                    {
                        LoggingService.UBICACIONTRAZA = (UtilTelemetry.UbicacionLogsYTrazas)valorInt2;
                    }
                }

            }

        }

    }
}
