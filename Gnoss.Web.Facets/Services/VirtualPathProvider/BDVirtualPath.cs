using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.Logica.ParametrosProyecto;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gnoss.Web.Services.VirtualPathProvider
{
    public class BDVirtualPath
    {
        private readonly LoggingService _loggingService;
        EntityContext _entityContext;
        ConfigService _configService;
        private IServicesUtilVirtuosoAndReplication _servicesUtilVirtuosoAndReplication;
        public static ConcurrentDictionary<string, string> ListaRutasVirtuales { get; } = new ConcurrentDictionary<string, string>();
        private static Dictionary<Guid, string> ListaHtmlsTemporales = new Dictionary<Guid, string>();

        public BDVirtualPath(EntityContext entityContext, LoggingService loggingService, ConfigService configService, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            _loggingService = loggingService;
            _entityContext = entityContext;
            _configService = configService;
            _servicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
        }

        public string FindPage(string virtualPath)
        {
            //if (virtualPath.Contains("/Views/") && !virtualPath.Contains("$$$"))
            //{
            //    virtualPath = virtualPath.Replace("Views", DirectorioVistas);
            //}

            _loggingService.AgregarEntrada($"FindPage {virtualPath}");
            string html = string.Empty;


            if (ListaRutasVirtuales.ContainsKey(virtualPath))
            {
                html = ListaRutasVirtuales[virtualPath];
            }
            else
            {
                string[] parametrosRuta = virtualPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                if (parametrosRuta.Length > 2 && parametrosRuta[0].Equals("Views") && parametrosRuta[1].Equals("TESTvistaTEST") && parametrosRuta.Last().EndsWith(".cshtml") && parametrosRuta.Last().Contains("$$$") && !parametrosRuta.Last().EndsWith(".Mobile.cshtml"))
                {
                    Guid idVistaTemporal = new Guid(parametrosRuta[2].Substring(0, parametrosRuta[2].IndexOf("$$$")));
                    if (BDVirtualPath.ListaHtmlsTemporales.ContainsKey(idVistaTemporal))
                    {
                        html = ListaHtmlsTemporales[idVistaTemporal];
                    }
                }
                else if (parametrosRuta.Length > 2 && parametrosRuta[0].Equals("Views") && parametrosRuta.Last().EndsWith(".cshtml") && !parametrosRuta.Last().EndsWith(".Mobile.cshtml"))
                {
                    string[] parametrosPagina = parametrosRuta.Last().Split(new string[] { "$$$" }, StringSplitOptions.RemoveEmptyEntries);

                    if (parametrosPagina.Length > 0)
                    {
                        Guid personalizacionID;

                        Guid.TryParse(parametrosPagina.Last().Substring(0, parametrosPagina.Last().IndexOf('.')), out personalizacionID);
                        if (!personalizacionID.Equals(Guid.Empty))
                        {
                            string tipoPagina = virtualPath.Substring(0, virtualPath.LastIndexOf('/')).Substring(7);

                            if (parametrosPagina[0].StartsWith("_"))
                            {
                                tipoPagina += parametrosPagina[0];
                            }

                            VistaVirtualCN vistaVirtualCN = new VistaVirtualCN(_entityContext, _loggingService, _configService, _servicesUtilVirtuosoAndReplication);

                            if (tipoPagina == "FichaRecurso")
                            {
                                string rdfType = parametrosPagina[0];
                                html = vistaVirtualCN.ObtenerHtmlParaVistaRDFTypeDePersonalizacion(personalizacionID, rdfType);
                            }
                            Guid personalizacionComponenteID;
                            if (Guid.TryParse(parametrosPagina[0], out personalizacionComponenteID) && !personalizacionComponenteID.Equals(Guid.Empty) && (tipoPagina == "CMSPagina" || tipoPagina.Equals("HomeComunidad")))
                            {
                                html = vistaVirtualCN.ObtenerHtmlParaVistaCMSDePersonalizacion(personalizacionID, personalizacionComponenteID);
                            }

                            if (tipoPagina == "Shared")
                            {
                                Guid.TryParse(parametrosPagina[0], out personalizacionComponenteID);

                                if (!personalizacionComponenteID.Equals(Guid.Empty))
                                {
                                    html = vistaVirtualCN.ObtenerHtmlParaVistaGadgetDePersonalizacion(personalizacionID, personalizacionComponenteID);
                                }
                            }

                            if (string.IsNullOrEmpty(html))
                            {
                                string realPath = virtualPath.Replace("$$$" + personalizacionID.ToString(), "");
                                if (realPath.EndsWith("/.cshtml"))
                                {
                                    realPath = realPath.Replace("/.cshtml", "/Index.cshtml");
                                }

                                html = vistaVirtualCN.ObtenerHtmlParaVistaDePersonalizacion(personalizacionID, realPath);

                                //if (string.IsNullOrEmpty(html) && virtualPath.Contains("/Views/") && !virtualPath.Contains("$$$"))
                                //{
                                //    virtualPath = virtualPath.Replace("Views", DirectorioVistas);
                                //}
                            }

                            try
                            {
                                ListaRutasVirtuales.TryAdd(virtualPath, html);
                            }
                            catch { }
                        }
                    }
                }
            }
            _loggingService.AgregarEntrada($"fin FindPage {virtualPath}");
            return html;
        }

        public static void LimpiarListasRutasVirtuales()
        {
            lock (ListaRutasVirtuales)
            {
                ListaRutasVirtuales.Clear();
            }
        }
    }
}
