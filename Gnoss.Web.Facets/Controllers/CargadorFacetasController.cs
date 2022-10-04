using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EncapsuladoDatos;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModel.Models.Faceta;
using Es.Riam.Gnoss.AD.EntityModel.Models.ParametroGeneralDS;
using Es.Riam.Gnoss.AD.Facetado;
using Es.Riam.Gnoss.AD.Facetado.Model;
using Es.Riam.Gnoss.AD.Parametro;
using Es.Riam.Gnoss.AD.ParametroAplicacion;
using Es.Riam.Gnoss.AD.ServiciosGenerales;
using Es.Riam.Gnoss.AD.Usuarios;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.CL.Facetado;
using Es.Riam.Gnoss.CL.ParametrosAplicacion;
using Es.Riam.Gnoss.CL.ParametrosProyecto;
using Es.Riam.Gnoss.CL.Seguridad;
using Es.Riam.Gnoss.CL.ServiciosGenerales;
using Es.Riam.Gnoss.CL.Tesauro;
using Es.Riam.Gnoss.Elementos.Facetado;
using Es.Riam.Gnoss.Elementos.Identidad;
using Es.Riam.Gnoss.Elementos.ParametroGeneralDSEspacio;
using Es.Riam.Gnoss.Elementos.ServiciosGenerales;
using Es.Riam.Gnoss.Elementos.Tesauro;
using Es.Riam.Gnoss.Logica.Documentacion;
using Es.Riam.Gnoss.Logica.Facetado;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.Logica.ServiciosGenerales;
using Es.Riam.Gnoss.Logica.Tesauro;
using Es.Riam.Gnoss.Recursos;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.UtilServiciosWeb;
using Es.Riam.Gnoss.Web.Controles;
using Es.Riam.Gnoss.Web.MVC.Models;
using Es.Riam.Gnoss.Web.MVC.Models.Administracion;
using Es.Riam.Interfaces;
using Es.Riam.Util;
using Gnoss.Web.Services.VirtualPathProvider;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static Es.Riam.Gnoss.UtilServiciosWeb.CargadorResultadosModel;

namespace ServicioCargaFacetas
{
    [ApiController]
    [Route("[controller]")]
    [EnableCors("_myAllowSpecificOrigins")]
    public class CargadorFacetasController : ControllerBase
    {

        #region Constantes

        private const string CLAUSULA_IF = "@@IF@@";
        private const string CLAUSULA_ENDIF = "@@ENDIF@@";
        private const string CLAUSULA_THEN = "@@THEN@@";
        private const string CLAUSULA_EXIST_FILTER = "ExistFilter";

        #endregion

        #region Miembros

        /// <summary>
        /// Obtiene si el ecosistema tiene una personalizacion de vistas
        /// </summary>
        private Guid? mPersonalizacionEcosistemaID = null;

        private bool? mComunidadExcluidaPersonalizacionEcosistema = null;
        private static object mBloqueoFiltrosSearchPersonalizados = new object();

        /// <summary>
        /// Fila de parámetros de aplicación
        /// </summary>
        private List<ParametroAplicacion> mParametrosAplicacionDS;

        /// <summary>
        /// Gestor facetas
        /// </summary>
        private GestionFacetas mGestorFacetas = null;

        /// <summary>
        /// Utilidades para idiomas
        /// </summary>
        private UtilIdiomas mUtilIdiomas;

        /// <summary>
        /// Indica si la petición la realiza un bot o no.
        /// </summary>
        private bool mEsBot = false;

        /// <summary>
        /// Este ID será proyecto en el caso de la búsqueda en comunidades o mygnoss, el id del perfil en el caso de contribuciones, etc.
        /// </summary>
        private string mGrafoID = "";

        /// <summary>
        /// ID del proyecto de origen para la búsqueda.
        /// </summary>
        private Guid mProyectoOrigenID;

        /// <summary>
        /// ID del proyecto virtual para la búsqueda.
        /// </summary>
        private Guid mProyectoVirtualID;

        /// <summary>
        /// Indica si la búsqueda es de tipo mapa.
        /// </summary>
        private bool mBusquedaTipoMapa;

        /// <summary>
        /// Indica si las facetas son pintadas para los formularios semánticos.
        /// </summary>
        private bool mFacetasEnFormSem;

        /// <summary>
        /// Indica (en caso de tener valor) el número de elementos a cargar por faceta
        /// </summary>
        private int? mNumElementosFaceta;

        private string mUrlPagina;

        /// <summary>
        /// Indica si no debe haber caché.
        /// </summary>
        private bool mSinCache;

        /// <summary>
        /// Indica si no debe haber privacidad.
        /// </summary>
        private bool mSinPrivacidad;

        /// <summary>
        /// Indica si no deben cargarse las propiedades semanticas.
        /// </summary>
        private bool mSinDatosExtra;

        /// <summary>
        /// Identificador de la pestanya actual
        /// </summary>
        private Guid mPestanyaActualID;

        private Dictionary<string, List<string>> mInformacionOntologias;

        #region Miembros contextos
        /// <summary>
        /// Nombre del filtro de contexto
        /// </summary>
        private string mFiltroContextoNombre;

        /// <summary>
        /// Parte select filtro contexto
        /// </summary>
        private string mFiltroContextoSelect;

        /// <summary>
        /// Parte where filtro contexto
        /// </summary>
        private string mFiltroContextoWhere;

        #endregion

        #region Proyecto

        private Guid mProyectoID = Guid.Empty;

        private Guid mOrganizacionID = new Guid("11111111-1111-1111-1111-111111111111");

        private Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.Proyecto mFilaProyecto;

        public DataWrapperProyecto mNivelesCertificacionDW;

        private GestionTesauro mGestorTesauro;

        /// <summary>
        /// DataSet con las pestañas del proyecto
        /// </summary>
        private DataWrapperProyecto mPestanyasProyectoDW = null;

        private static ConcurrentDictionary<Guid, Guid> mListaOrganizacionIDPorProyectoID = new ConcurrentDictionary<Guid, Guid>();

        #endregion

        #region Miembros del buscador facetado

        //private ConfiguracionFacetadoDS mTConfiguracionOntologia = null;

        Dictionary<string, List<string>> mListaFiltros = null;
        Dictionary<string, List<string>> mListaFiltrosConGrupos = null;

        List<string> mListaItemsBusqueda = null;

        Dictionary<string, List<string>> mListaFiltrosFacetasNombreReal = null;

        private List<string> mListaItemsBusquedaExtra = null;

        /// <summary>
        /// Lista de filtros establecidos desde las facetas por el usuario
        /// </summary>
        Dictionary<string, List<string>> mListaFiltrosFacetasUsuario;
        /// <summary>
        /// Lista de filtros establecidos desde las facetas por el usuario
        /// </summary>
        Dictionary<string, List<string>> mListaFiltrosFacetasUsuarioConGrupos;

        Dictionary<string, bool> mListaPrivacidadProyecto = new Dictionary<string, bool>();

        FacetadoDS mFacetadoDS = null;

        /// <summary>
        /// DataSet de facetas auxiliares por faceta.
        /// </summary>
        Dictionary<string, List<KeyValuePair<string, FacetadoDS>>> mFacetadoDSAuxPorFaceta = new Dictionary<string, List<KeyValuePair<string, FacetadoDS>>>();

        FacetadoCL mFacetadoCL = null;

        //ConfiguracionFacetadoDS mConfiguracionDS = new ConfiguracionFacetadoDS();

        /// <summary>
        /// Formularios semánticos del proyecto
        /// </summary>
        private List<string> mFormulariosSemanticos = null;

        /// <summary>
        /// Verdad si hay que cargar por defecto el árbol de categorías
        /// </summary>
        private bool mCargarArbolCategorias = true;

        /// <summary>
        /// Indica si hay que cargar las facetas de la home de un catálogo.
        /// </summary>
        private bool mFacetasHomeCatalogo;
        bool mEsProyectoCatalogo = false;

        /// <summary>
        /// Sólo es True si hay que mostrar la faceta explora...
        /// </summary>
        private bool mNecesarioMostarTiposElementos = false;

        /// <summary>
        /// Sólo se muestra la faceta estado de las contribuciones de un usuario si es él mismo el que las está viendo
        /// </summary>
        private bool mMostrarFacetaEstado = true;

        /// <summary>
        /// Parmas adicionales.
        /// </summary>
        string mParametros_adiccionales;

        /// <summary>
        /// Pestaña para la faceta CMS
        /// </summary>
        string mPestanyaFacetaCMS = null;

        /// <summary>
        /// Formularios semánticos del proyecto
        /// </summary>
        private List<string> mTablasNoCargadas = new List<string>();

        #endregion

        /// <summary>
        /// Identidad del usuario actual
        /// </summary>
        private Identidad mIdentidadActual;

        #region Parámetros de la búsqueda

        string mLanguageCode = "es";
        Guid mIdentidadID;
        Guid mPerfilIdentidadID;
        Guid? mOrganizacionPerfilID;
        bool mAdministradorQuiereVerTodasLasPersonas = false;
        bool mEsMyGnoss = false;
        bool mEstaEnProyecto = false;
        bool mEsUsuarioInvitado = false;
        bool mPrimeraCarga = false;

        private TipoBusqueda mTipoBusqueda;
        private int mNumeroFacetas;
        string mUbicacionBusqueda;

        /// <summary>
        /// Faceta que hay que cargar (sólo si el usuario ha pinchado en ver mas, sino será null)
        /// </summary>
        private string mFaceta = null;

        private bool mEsRefrescoCache = false;

        private ParametroGeneral mParametrosGenerales = null;

        private Dictionary<string, string> mParametroProyecto = null;

        private Proyecto mProyectoSeleccionado = null;

        private string mBaseUrl = "";

        #endregion

        /// <summary>
        /// Lista con las claves de facetas que son tesauro semántico y su datos en un dataSet.
        /// </summary>
        private Dictionary<string, FacetadoDS> mTesauroSemDSFaceta;

        private const decimal PORCENTAJE_APLICAR_TOTAL_RESULTADOS_CALCULO_RANGOS = 0.3M;
        private const decimal PORCENTAJE_APLICAR_MINIMO_RESULTADOS_AGRUPAR = 0.05M;

        /// <summary>
        /// Diccionario con los números romanos por siglo
        /// </summary>
        private Dictionary<int, string> mDicNumerosRomanos = new Dictionary<int, string>();

        /// Diccionario con los filtros tipo 'search' personalizados
        /// la clave es el nombre del filtro y el valor es 'WhereSPARQL','OrderBySPARQL','WhereFacetasSPARQL', 'OmitirRdfType'
        /// </summary>
        Dictionary<string, Tuple<string, string, string, bool>> mFiltrosSearchPersonalizados;

        #endregion

        private EntityContext mEntityContext;
        private LoggingService mLoggingService;
        private RedisCacheWrapper mRedisCacheWrapper;
        private ConfigService mConfigService;
        private VirtuosoAD mVirtuosoAD;
        private GnossCache mGnossCache;
        private UtilServicios mUtilServicios;
        private IHttpContextAccessor mHttpContextAccessor;
        private ICompositeViewEngine mViewEngine;
        private ControladorBase mControladorBase;
        private UtilServiciosFacetas mUtilServiciosFacetas;
        private IHostingEnvironment mEnv;
        private IServicesUtilVirtuosoAndReplication mServicesUtilVirtuosoAndReplication;

        #region Constructor

        public CargadorFacetasController(EntityContext entityContext, LoggingService loggingService, RedisCacheWrapper redisCacheWrapper, ConfigService configService, VirtuosoAD virtuosoAD, GnossCache gnossCache, UtilServicios utilServicios, IHttpContextAccessor httpContextAccessor, ICompositeViewEngine viewEngine, IHostingEnvironment env, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
            : base(loggingService, configService, entityContext, redisCacheWrapper, gnossCache, virtuosoAD, httpContextAccessor, servicesUtilVirtuosoAndReplication)
        {
            mEntityContext = entityContext;
            mLoggingService = loggingService;
            mRedisCacheWrapper = redisCacheWrapper;
            mConfigService = configService;
            mVirtuosoAD = virtuosoAD;
            mGnossCache = gnossCache;
            mUtilServicios = utilServicios;
            mHttpContextAccessor = httpContextAccessor;
            mViewEngine = viewEngine;
            mEnv = env;
            mServicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
            mControladorBase = new ControladorBase(loggingService, configService, entityContext, redisCacheWrapper, gnossCache, virtuosoAD, httpContextAccessor, mServicesUtilVirtuosoAndReplication);
            mUtilServiciosFacetas = new UtilServiciosFacetas(loggingService, entityContext, configService, redisCacheWrapper, virtuosoAD, mServicesUtilVirtuosoAndReplication);
        }

        #endregion


        #region Metodos Web
        [HttpGet, HttpPost]
        [Route("LimpiarCache")]
        public ActionResult LimpiarCache()
        {
            //TODO Javier
            //MyVirtualPathProvider.listaRutasVirtuales.Clear();
            return View();
        }

        [HttpGet, HttpPost]
        [Route("InvalidarCacheLocal")]
        public void InvalidarCacheLocal([FromForm] string pProyectoID)
        {
            mGnossCache.VersionarCacheLocal(new Guid(pProyectoID));
        }

        [NonAction]
        private string ObtenerUrlPorFiltros(TipoBusqueda pTipoBusqueda, string pParametros)
        {
            string url = "";

            if (pTipoBusqueda == TipoBusqueda.Mensajes)
            {
                url = $"/{UtilIdiomas.GetText("URLSEM", "MENSAJES")}";
            }

            if (!string.IsNullOrEmpty(pParametros))
            {
                url += $"?{pParametros.Replace('|', '&')}";
            }

            return url;
        }

        /// <summary>
        /// Método para cargar las facetas
        /// </summary>
        /// <param name="pProyectoID">Identificador del proyecto</param>
        /// <param name="pEstaEnProyecto">Booleano que indica si el usuario está en el proyecto</param>
        /// <param name="pEsUsuarioInvitado">Booleano que indica si el usuario es el invitado</param>
        /// <param name="pIdentidadID">Identificador de la identidad</param>
        /// <param name="pParametros">Parametros</param>
        /// <param name="pUbicacionBusqueda">Ubicacion de la busqueda</param>
        /// <param name="pLanguageCode">Codigo del idioma</param>
        /// <param name="pAdministradorVeTodasPersonas">Booleano que indica si el administrador quiere ver todas las personas</param>
        /// <param name="pTipoBusqueda">Tipo de busqueda</param>
        /// <param name="pNumeroFacetas">Número de facetas</param>
        /// <param name="pFaceta">Faceta</param>
        /// <param name="pGrafo">Grafo de búsqueda</param>
        /// <param name="pParametros_adiccionales">Parametros adicionales</param>
        /// <param name="pFiltroContexto">Filtro contexto</param>
        /// <param name="pUrlPaginaActual">URL de la página actual</param>
        /// <param name="pUsarMasterParaLectura">Usar master para lectura</param>
        /// <returns></returns>
        [HttpGet, HttpPost]
        [Route("CargarFacetas")]
        public ActionResult CargarFacetas([FromForm] string pProyectoID, [FromForm] bool pEstaEnProyecto, [FromForm] bool pEsUsuarioInvitado, [FromForm] string pIdentidadID, [FromForm] string pParametros, [FromForm] string pUbicacionBusqueda, [FromForm] string pLanguageCode, [FromForm] bool pAdministradorVeTodasPersonas, [FromForm] short pTipoBusqueda, [FromForm] int? pNumeroFacetas, [FromForm] string pFaceta, [FromForm] string pGrafo, [FromForm] string pParametros_adiccionales, [FromForm] string pFiltroContexto, [FromForm] string pUrlPaginaActual, [FromForm] bool pUsarMasterParaLectura, [FromForm] bool? pJson, [FromForm] string tokenAfinidad)
        {
            try
            {
                ProyectoCN proyectoCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                if (pParametros == null)
                {
                    pParametros = "";
                }
                if (pFaceta == null)
                {
                    pFaceta = "";
                }
                if (pParametros_adiccionales == null)
                {
                    pParametros_adiccionales = "";
                }
                if (pUbicacionBusqueda == null)
                {
                    pUbicacionBusqueda = "";
                }
                if (pFiltroContexto == null)
                {
                    pFiltroContexto = "";
                }
                if (pNumeroFacetas == null)
                {
                    pNumeroFacetas = 1;
                }
                if (!string.IsNullOrEmpty(tokenAfinidad))
                {
                    new SeguridadCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication).ObtenerConexionAfinidad(tokenAfinidad.Replace("\"", ""));
                }

                #region Obtenemos parámetros

                pProyectoID = pProyectoID.Replace("\"", "");
                pIdentidadID = pIdentidadID.Replace("\"", "");
                pParametros = pParametros.Replace("\"", "");
                pUbicacionBusqueda = pUbicacionBusqueda.Replace("\"", "");
                pLanguageCode = pLanguageCode.Replace("\"", "");
                pFaceta = pFaceta.Replace("\"", "");
                pGrafo = pGrafo?.Replace("\"", "");
                pParametros_adiccionales = pParametros_adiccionales.Replace("\"", "");
                mLanguageCode = pLanguageCode;

                if (!string.IsNullOrEmpty(pUrlPaginaActual))
                {
                    mUrlPagina = pUrlPaginaActual.Replace("\"", "");
                }
                else if (Request.Headers.ContainsKey("Referer"))
                {
                    mUrlPagina = Request.Headers["Referer"].ToString();
                }

                string idioma = "/" + pLanguageCode.ToLower();
                if (ProyectoSeleccionado == null)
                {
                    mProyectoID = new Guid(pProyectoID);
                }
                if (pLanguageCode.ToLower() == ParametrosGenerales.IdiomaDefecto || (string.IsNullOrEmpty(ParametrosGenerales.IdiomaDefecto) && pLanguageCode.ToLower().Equals("es")))
                {
                    idioma = "";
                }

                Guid proyectoID = new Guid(pProyectoID);
                Guid identidadID = new Guid(pIdentidadID);

                if (!string.IsNullOrEmpty(mUrlPagina))
                {
                    ProyectoCL paramCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    Dictionary<string, string> dicParametros = paramCL.ObtenerParametrosProyecto(proyectoID);
                    paramCL.Dispose();
                    mUrlPagina = new Uri(mUrlPagina).PathAndQuery;

                    //URL de identidad
                    if (mUrlPagina.ToLower().StartsWith($"{idioma}/{UtilIdiomas.GetText("URLSEM", "IDENTIDAD")}/"))
                    {
                        mUrlPagina = mUrlPagina.Substring(($"{idioma}/{UtilIdiomas.GetText("URLSEM", "IDENTIDAD")}/").Length);
                        if (mUrlPagina.IndexOf('/') > 0)
                        {
                            mUrlPagina = mUrlPagina.Substring(mUrlPagina.IndexOf('/'));
                        }
                        else
                        {
                            mUrlPagina = "";
                        }
                    }

                    //URL de comunidad 
                    else if (mUrlPagina.ToLower().StartsWith($"{idioma}/{UtilIdiomas.GetText("URLSEM", "COMUNIDAD")}/"))
                    {
                        mUrlPagina = mUrlPagina.Substring(($"{idioma}/{UtilIdiomas.GetText("URLSEM", "COMUNIDAD")}/").Length);
                        if (mUrlPagina.IndexOf('/') > 0)
                        {
                            mUrlPagina = mUrlPagina.Substring(mUrlPagina.IndexOf('/'));
                        }
                        else
                        {
                            mUrlPagina = "";
                        }
                    }
                    //URL de 'mygnoss' sin comunidad (pero no se trata de una comunidad sin nombre corto)
                    else if (pLanguageCode.ToLower() != "es" && mUrlPagina.ToLower().StartsWith(idioma + "/") && !(dicParametros.ContainsKey(ParametroAD.ProyectoSinNombreCortoEnURL) && (dicParametros[ParametroAD.ProyectoSinNombreCortoEnURL] == "1" || dicParametros[ParametroAD.ProyectoSinNombreCortoEnURL] == "true")))
                    {
                        mUrlPagina = mUrlPagina.Substring(($"{idioma}/").Length);
                        if (mUrlPagina.IndexOf('/') > 0)
                        {
                            mUrlPagina = mUrlPagina.Substring(mUrlPagina.IndexOf('/'));
                        }
                        else
                        {
                            mUrlPagina = "";
                        }
                    }
                    else if (mUrlPagina.ToLower().StartsWith(idioma + "/"))
                    {
                        mUrlPagina = mUrlPagina.Substring((idioma).Length);
                    }
                }
                else
                {
                    mUrlPagina = ObtenerUrlPorFiltros((TipoBusqueda)pTipoBusqueda, pParametros);
                }

                //Ponemos el utilidiomas a null para que se cargue con el proyecto seleccionado y la personalizacion de las vistas
                UtilIdiomas = null;

                if (!string.IsNullOrEmpty(pFiltroContexto) && pFiltroContexto.Length > 2 && pFiltroContexto.StartsWith("\"") && pFiltroContexto.EndsWith("\""))
                {
                    pFiltroContexto = pFiltroContexto.Substring(1, pFiltroContexto.Length - 2);
                }
                pFiltroContexto = HttpUtility.UrlDecode(pFiltroContexto);

                if (pFaceta == "null" || string.IsNullOrEmpty(pFaceta))
                {
                    pFaceta = null;
                }
                #endregion

                bool esMovil = mControladorBase.RequestParams("esMovil") == "true";

                //Obtenemos las facetas y los filtros
                FacetedModel facModel = CargarFacetasInt(proyectoID, pEstaEnProyecto, pEsUsuarioInvitado, identidadID, pParametros, pUbicacionBusqueda, pLanguageCode, pAdministradorVeTodasPersonas, (TipoBusqueda)pTipoBusqueda, pNumeroFacetas.Value, pFaceta, pGrafo, pParametros_adiccionales, pFiltroContexto, esMovil);

                string urlBaseFacetas = "";
                if (ProyectoSeleccionado.Clave.Equals(ProyectoAD.MetaProyecto))
                {
                    ProyectoCL proyCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    urlBaseFacetas = proyCL.ObtenerURLPropiaProyecto(mProyectoVirtualID, mControladorBase.IdiomaUsuario).TrimEnd('/') + idioma;
                    if (IdentidadActual.TrabajaConOrganizacion)
                    {
                        urlBaseFacetas = $"{urlBaseFacetas.TrimEnd('/')}/{UtilIdiomas.GetText("URLSEM", "IDENTIDAD")}/{IdentidadActual.PerfilUsuario.NombreCortoOrg}";
                    }
                }
                else
                {
                    urlBaseFacetas = mControladorBase.UrlsSemanticas.ObtenerURLComunidad(UtilIdiomas, BaseURLIdioma, ProyectoSeleccionado.NombreCorto);
                }
                urlBaseFacetas = urlBaseFacetas.TrimEnd('/');

                foreach (FacetModel faceta in facModel.FacetList)
                {
                    foreach (FacetItemModel itemFaceta in faceta.FacetItemList)
                    {
                        MontarFiltroItemFaceta(itemFaceta, urlBaseFacetas);
                    }
                }

                foreach (FacetItemModel itemFilter in facModel.FilterList)
                {
                    itemFilter.Filter = urlBaseFacetas + itemFilter.Filter;
                }

                if (pJson.HasValue && pJson.Value)
                {
                    string respuesta = System.Text.Json.JsonSerializer.Serialize(facModel);
                    return Content(respuesta);
                }

                #region Construimos el resultado
                ViewBag.UtilIdiomas = UtilIdiomas;
                ViewBag.BaseUrlStatic = BaseURLStatic;
                ViewData.Model = facModel;

                string parametros = "";

                if (mListaFiltros.Count > 0)
                {
                    foreach (string clave in mListaFiltros.Keys)
                    {
                        foreach (string valor in mListaFiltros[clave])
                        {
                            parametros += $"|{clave}={valor}";
                        }
                    }
                }

                if (!string.IsNullOrEmpty(parametros))
                {
                    parametros = parametros.Substring(1, parametros.Length - 1);
                }

                ViewBag.Parametros = parametros;
                ViewBag.GrafoID = mGrafoID;
                ViewBag.FiltroContextoWhere = mFiltroContextoWhere;

                CargarPersonalizacion(mProyectoVirtualID);

                string funcionCallBack = HttpContext.Request.Query["callback"];
                bool jsonRequest = mHttpContextAccessor.HttpContext.Request.Headers.ContainsKey("Accept") && Request.Headers["Accept"].Equals("application/json");

                if (jsonRequest)
                {
                    string respuesta = string.Empty;

                    using (MemoryStream input = new MemoryStream())
                    {
                        BinaryFormatter bformatter = new BinaryFormatter();
                        bformatter.Serialize(input, facModel);
                        input.Seek(0, SeekOrigin.Begin);

                        using (MemoryStream output = new MemoryStream())
                        using (DeflateStream deflateStream = new DeflateStream(output, CompressionMode.Compress))
                        {
                            input.CopyTo(deflateStream);
                            deflateStream.Close();

                            respuesta = Convert.ToBase64String(output.ToArray());
                        }
                    }
                    if (mHttpContextAccessor.HttpContext.Request.Headers["User-Agent"].Contains("GnossInternalRequest"))
                    {
                        respuesta = SerializeViewData(respuesta);
                    }
                    return Content(respuesta);
                }
                else if (string.IsNullOrEmpty(funcionCallBack))
                {
                    return View("CargarFacetas");
                }
                else
                {
                    string resultado = "";
                    using (StringWriter sw = new StringWriter())
                    {
                        ViewEngineResult viewResult = mViewEngine.FindView(ControllerContext, ObtenerNombreVista("CargarFacetas"), false);
                        if (viewResult.View == null) throw new Exception("View not found: CargarFacetas");
                        ViewContext viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, sw, new HtmlHelperOptions());
                        viewResult.View.RenderAsync(viewContext);
                        resultado = sw.GetStringBuilder().ToString();
                    }

                    //Devuelvo la respuesta en el response de la petición
                    HttpContext.Response.ContentType = "text/plain";
                    HttpContext.Response.WriteAsync($"{funcionCallBack}({{\"d\":{System.Text.Json.JsonSerializer.Serialize(resultado)}}});");
                }
                #endregion
            }
            catch (ThreadAbortException) { }
            catch (Exception ex)
            {
                mLoggingService.GuardarLogError(ex);
                throw;
            }

            return new EmptyResult();
        }

        /// <summary>
        /// Método para refrescar las facetas
        /// </summary>
        /// <param name="pProyectoID">Identificador del proyecto</param>
        /// <param name="pEstaEnProyecto">Booleano que indica si el usuario está en el proyecto</param>
        /// <param name="pEsUsuarioInvitado">Booleano que indica si el usuario es el invitado</param>
        /// <param name="pUbicacionBusqueda">Ubicacion de la busqueda</param>
        /// <param name="pLanguageCode">Codigo del idioma</param>
        /// <param name="pTipoBusqueda">Tipo de busqueda</param>
        /// <param name="pNumeroFacetas">Número de facetas</param>
        /// <param name="pParametros_adiccionales">Parametros adicionales</param>
        /// <param name="pEsBot">Booleano que indica si es bot</param>
        /// <param name="pFaceta">Faceta</param>
        /// <returns></returns>
        [HttpGet, HttpPost]
        [Route("RefrescarFacetas")]
        public ActionResult RefrescarFacetas(string pProyectoID, bool pEstaEnProyecto, bool pEsUsuarioInvitado, string pUbicacionBusqueda, string pLanguageCode, short pTipoBusqueda, int pNumeroFacetas, string pParametros_adiccionales, bool pEsBot, string pFaceta)
        {
            try
            {
                if (string.IsNullOrEmpty(pProyectoID))
                {
                    pProyectoID = Request.Form["pProyectoID"];
                    pEstaEnProyecto = Request.Form["pEstaEnProyecto"].ToString().ToLower() == "true" ? true : false;
                    pEsUsuarioInvitado = Request.Form["pEsUsuarioInvitado"].ToString().ToLower() == "true" ? true : false;
                    pUbicacionBusqueda = Request.Form["pUbicacionBusqueda"];
                    pLanguageCode = Request.Form["pLanguageCode"];
                    pTipoBusqueda = short.Parse(Request.Form["pTipoBusqueda"]);
                    pNumeroFacetas = int.Parse(Request.Form["pNumeroFacetas"]);
                    pEsBot = Request.Form["pEsBot"].ToString().ToLower() == "true" ? true : false;
                    pParametros_adiccionales = Request.Form["pParametros_adiccionales"];
                    pFaceta = Request.Form["pFaceta"];
                }

                #region Obtenemos parámetros

                pProyectoID = pProyectoID.Replace("\"", "");
                pUbicacionBusqueda = pUbicacionBusqueda.Replace("\"", "");
                pLanguageCode = pLanguageCode.Replace("\"", "");
                pFaceta = pFaceta.Replace("\"", "");
                pParametros_adiccionales = pParametros_adiccionales.Replace("\"", "");

                if (pFaceta == "null" || string.IsNullOrEmpty(pFaceta))
                {
                    pFaceta = null;
                }

                mEsBot = pEsBot;
                mEsRefrescoCache = true;
                mEsUsuarioInvitado = pEsUsuarioInvitado;
                mEstaEnProyecto = pEstaEnProyecto;
                mUbicacionBusqueda = pUbicacionBusqueda;
                mLanguageCode = pLanguageCode;

                mProyectoID = new Guid(pProyectoID);
                if (mEsBot)
                {
                    EstablecerOrganizacionIDDeProyectoID(mProyectoID);

                    string pestaña = "";
                    if (pParametros_adiccionales != "")
                    {
                        pestaña = pParametros_adiccionales.Replace("esBot=true", "");

                        if (pestaña.Contains("|"))
                        {
                            char[] separadores = { '|' };
                            string[] filtros = pestaña.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                            pestaña = "";
                            string separador = "";

                            foreach (string filtro in filtros)
                            {
                                //Si hay algun orden, lo quito de la pestaña
                                if (!filtro.StartsWith("orden=") && !filtro.StartsWith("ordenarPor="))
                                {
                                    pestaña += separador + filtro;
                                    separador = "|";
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(pestaña))
                    {
                        if ((TipoBusqueda)pTipoBusqueda == TipoBusqueda.BusquedaAvanzada)
                        {
                            pestaña = UtilIdiomas.GetText("URLSEM", "BUSQUEDAAVANZADA");
                        }
                        else if ((TipoBusqueda)pTipoBusqueda == TipoBusqueda.Debates)
                        {
                            pestaña = UtilIdiomas.GetText("URLSEM", "DEBATES");
                        }
                        else if ((TipoBusqueda)pTipoBusqueda == TipoBusqueda.Preguntas)
                        {
                            pestaña = UtilIdiomas.GetText("URLSEM", "PREGUNTAS");
                        }
                        else if ((TipoBusqueda)pTipoBusqueda == TipoBusqueda.Encuestas)
                        {
                            pestaña = UtilIdiomas.GetText("URLSEM", "ENCUESTAS");
                        }
                        else if (mProyectoID != ProyectoAD.MetaProyecto && !ParametrosGenerales.PestanyaRecursosVisible)
                        {
                            pestaña = UtilIdiomas.GetText("URLSEM", "BUSQUEDAAVANZADA");
                        }
                    }

                    int i = 0;
                    string nombreSem = "";
                    if (!string.IsNullOrEmpty(pestaña))
                    {
                        foreach (Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.ProyectoPestanyaBusqueda filaPestaña in ProyectoSeleccionado.GestorProyectos.DataWrapperProyectos.ListaProyectoPestanyaBusqueda)
                        {
                            if (pestaña == filaPestaña.CampoFiltro)
                            {
                                nombreSem = filaPestaña.ProyectoPestanyaMenu.Ruta;
                                break;
                            }
                        }
                    }

                    mUrlPagina = $"/{nombreSem}";
                    if (pestaña == GetText("URLSEM", "BUSQUEDAAVANZADA"))
                    {
                        mUrlPagina = $"/{GetText("URLSEM", "BUSQUEDAAVANZADA")}";
                    }
                    else if (pestaña == GetText("URLSEM", "DEBATES"))
                    {
                        mUrlPagina = $"/{GetText("URLSEM", "DEBATES")}";
                    }
                    else if (pestaña == GetText("URLSEM", "PREGUNTAS"))
                    {
                        mUrlPagina = $"/{GetText("URLSEM", "PREGUNTAS")}";
                    }
                    else if (pestaña == GetText("URLSEM", "ENCUESTAS"))
                    {
                        mUrlPagina = $"/{GetText("URLSEM", "ENCUESTAS")}";
                    }
                }

                string parametros = "";
                string identidadID = UsuarioAD.Invitado.ToString();
                string grafo = mProyectoID.ToString();
                if (pTipoBusqueda.Equals((short)TipoBusqueda.Mensajes) && !string.IsNullOrEmpty(pParametros_adiccionales))
                {
                    parametros = pParametros_adiccionales.Split('|')[0];
                    identidadID = pParametros_adiccionales.Split('|')[1];
                    grafo = pParametros_adiccionales.Split('|')[2];

                    pParametros_adiccionales = "";
                    mUrlPagina = ObtenerUrlPorFiltros(TipoBusqueda.Mensajes, parametros);
                }

                #endregion

                bool esMovil = mControladorBase.RequestParams("esMovil") == "true";

                CargarFacetasInt(mProyectoID, pEstaEnProyecto, pEsUsuarioInvitado, new Guid(identidadID), parametros, pUbicacionBusqueda, pLanguageCode, false, (TipoBusqueda)pTipoBusqueda, pNumeroFacetas, pFaceta, grafo, pParametros_adiccionales, "", esMovil);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex)
            {
                mUtilServicios.EnviarErrorYGuardarLog($"Error: {ex.Message}\r\nPila: {ex.StackTrace}", "error", mEsBot);
            }

            return new EmptyResult();
        }

        [NonAction]
        private string SerializeViewData(string json)
        {
            ViewBag.ControladorProyectoMVC = null;
            UtilIdiomasSerializable aux = ViewBag.UtilIdiomas.GetUtilIdiomas();
            ViewBag.UtilIdiomas = aux;

            JsonSerializerSettings jsonSerializerSettingsVB = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full
            };
            Dictionary<string, object> dic = ViewData.Where(k => !k.Key.Equals("LoggingService")).ToDictionary(k => k.Key, v => v.Value);
            string jsonViewData = JsonConvert.SerializeObject(dic, jsonSerializerSettingsVB);

            return $"{json}{{ComienzoJsonViewData}}{jsonViewData}";
        }

        /// <summary>
        /// Carga las facetas para una búsqueda determinada
        /// </summary>
        /// <param name="pProyectoID">Identificador del proyecto en el que se encuentra el usuario</param>
        /// <param name="pEstaEnProyecto">Verdad si el usuario es miembro del proyecto</param>
        /// <param name="pEsUsuarioInvitado">Verdad si el usuario es el usuario invitado</param>
        /// <param name="pIdentidadID">Identificador de la identidad del usuario que hace la búsqueda</param>
        /// <param name="pParametros">Parámetros de la búsqueda</param>
        /// <param name="pUbicacionBusqueda">Ubicación de la búsqueda (Particular, MyGnoss...)</param>
        /// <param name="pLanguageCode">Código del idioma del usuario</param>
        /// <param name="pAdministradorVeTodasPersonas">Verdad si el administrador quiere ver todas las personas</param>
        /// <param name="pTipoBusqueda">Tipo de búsqueda (Recursos, Blogs, Personas y organizaciones...)</param>
        /// <param name="pNumeroFacetas">Número de facetas a cargar</param>
        /// <param name="pFaceta">Faceta que se quiere cargar (solo para cargar UNA faceta, NULL para cargar todas)</param>
        /// <param name="pGrafo">Grafo en el que se busca (del proyecto, del perfil...)</param>
        /// <param name="pParametros_adiccionales">Parámetros adiccionales para ésta búsqueda (no intruducidos por el usuario)</param>
        /// <param name="pFiltroContexto"></param>
        /// <returns></returns>
        [HttpGet, HttpPost]
        [Route("CargarFacetasInt")]
        public FacetedModel CargarFacetasInt(Guid pProyectoID, bool pEstaEnProyecto, bool pEsUsuarioInvitado, Guid pIdentidadID, string pParametros, string pUbicacionBusqueda, string pLanguageCode, bool pAdministradorVeTodasPersonas, TipoBusqueda pTipoBusqueda, int pNumeroFacetas, string pFaceta, string pGrafo, string pParametros_adiccionales, string pFiltroContexto, bool pEsMovil)
        {
            mProyectoID = pProyectoID;
            List<FacetModel> listaFacetas = new List<FacetModel>();
            List<FacetItemModel> listaFiltros = new List<FacetItemModel>();

            IniciarTraza();
            mLoggingService.AgregarEntrada("Empieza CargarFacetas");

            if (!pProyectoID.Equals(Guid.Empty))
            {
                mUtilServicios.ComprobacionCambiosCachesLocales(pProyectoID);
            }

            if (mTipoBusqueda == TipoBusqueda.VerRecursosPerfil)
            {
                try
                {
                    pParametros_adiccionales += $"|skos:ConceptID=gnoss:{GestorTesauro.TesauroDW.ListaTesauroUsuario.FirstOrDefault().CategoriaTesauroPublicoID.ToString().ToUpper()}|gnoss:hasEstadoPP=Publicado";
                }
                catch (Exception)
                {
                    pParametros_adiccionales += $"|skos:ConceptID=gnoss:{GestorTesauro.TesauroDW.ListaTesauroOrganizacion.FirstOrDefault().CategoriaTesauroPublicoID.ToString().ToUpper()}|gnoss:hasEstadoPP=Publicado";
                }
            }


            #region Parametros Extra petición faceta
            if (pParametros_adiccionales.Contains("NumElementosFaceta="))
            {
                string trozo1 = pParametros_adiccionales.Substring(0, pParametros_adiccionales.IndexOf("NumElementosFaceta="));
                string NumElementosFaceta = pParametros_adiccionales.Substring(pParametros_adiccionales.IndexOf("NumElementosFaceta="));
                string trozo2 = NumElementosFaceta.Substring(NumElementosFaceta.IndexOf("|") + 1);
                NumElementosFaceta = NumElementosFaceta.Substring(0, NumElementosFaceta.IndexOf("|"));
                NumElementosFaceta = NumElementosFaceta.Substring(NumElementosFaceta.IndexOf("=") + 1);
                mNumElementosFaceta = int.Parse(NumElementosFaceta);
                pParametros_adiccionales = trozo1 + trozo2;
            }
            #endregion

            #region Sin caché

            if (pParametros_adiccionales.Contains("sinCache=true"))
            {
                mSinCache = true;
                pParametros_adiccionales = pParametros_adiccionales.Replace("sinCache=true", "");
            }

            #endregion

            #region Sin privacidad

            if (pParametros_adiccionales.Contains("sinPrivacidad=true"))
            {
                mSinPrivacidad = true;
                pParametros_adiccionales = pParametros_adiccionales.Replace("sinPrivacidad=true", "");
            }

            #endregion

            #region Sin datos extra

            if (pParametros_adiccionales.Contains("sinDatosExtra=true"))
            {
                mSinDatosExtra = true;
                pParametros_adiccionales = pParametros_adiccionales.Replace("sinDatosExtra=true", "");
            }

            #endregion

            #region Es bot

            if (pParametros_adiccionales.Contains("esBot=true"))
            {
                mEsBot = true;
                pParametros_adiccionales = pParametros_adiccionales.Replace("esBot=true", "");
            }

            #endregion

            #region PestanyaActual

            if (pParametros_adiccionales.Contains("PestanyaActualID="))
            {
                string trozo1 = pParametros_adiccionales.Substring(0, pParametros_adiccionales.IndexOf("PestanyaActualID="));
                string trozoRutaPestanya = pParametros_adiccionales.Substring(pParametros_adiccionales.IndexOf("PestanyaActualID="));
                string trozo2 = trozoRutaPestanya.Substring(trozoRutaPestanya.IndexOf("|") + 1);
                trozoRutaPestanya = trozoRutaPestanya.Substring(0, trozoRutaPestanya.IndexOf("|"));

                mPestanyaActualID = new Guid(trozoRutaPestanya.Substring(trozoRutaPestanya.IndexOf("=") + 1));
                pParametros_adiccionales = trozo1 + trozo2;

                if (FilaPestanyaBusquedaActual != null && FilaPestanyaBusquedaActual.CampoFiltro.Contains("rdf:type") && pParametros.Contains("rdf:type"))
                {
                    //Eliminamos los rdf:type de parametros que no estén configurados en la pestaña (si hay rdf:type configurado en la pestanya)
                    List<string> listaTiposDePestanya = new List<string>();
                    string[] camposFiltrosPestanya = FilaPestanyaBusquedaActual.CampoFiltro.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string tipoPestanya in camposFiltrosPestanya)
                    {
                        string tempTipoPestanya = tipoPestanya;
                        if (tempTipoPestanya.StartsWith("("))
                        {
                            tempTipoPestanya = tempTipoPestanya.Substring(1);
                            tempTipoPestanya = tempTipoPestanya.Substring(0, tempTipoPestanya.Length - 1);
                            string[] camposSubFiltroPestanya = tempTipoPestanya.Split('@');

                            string rdfTypeCondicionado = string.Empty;
                            foreach (string campoSubFiltro in camposSubFiltroPestanya)
                            {
                                if (campoSubFiltro.StartsWith("rdf:type") && !listaTiposDePestanya.Contains(campoSubFiltro))
                                {
                                    listaTiposDePestanya.Add(campoSubFiltro);
                                    rdfTypeCondicionado = campoSubFiltro;
                                }
                                else if (pParametros.Contains(rdfTypeCondicionado))
                                {
                                    pParametros += "|" + campoSubFiltro;
                                }
                            }
                        }
                        else if (tempTipoPestanya.StartsWith("rdf:type") && !listaTiposDePestanya.Contains(tempTipoPestanya))
                        {
                            listaTiposDePestanya.Add(tempTipoPestanya);
                        }
                    }

                    List<string> listaTiposDeParametros = new List<string>();
                    string[] camposFiltrosParametros = pParametros.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string tipoParametro in camposFiltrosParametros)
                    {
                        if (tipoParametro.StartsWith("rdf:type") && !listaTiposDeParametros.Contains(tipoParametro))
                        {
                            listaTiposDeParametros.Add(tipoParametro);
                        }
                    }

                    foreach (string tipoParametro in listaTiposDeParametros)
                    {
                        if (!listaTiposDePestanya.Contains(tipoParametro))
                        {
                            pParametros = pParametros.Replace(tipoParametro, "");
                        }
                    }
                }
            }

            #endregion

            #region ProyectoOrigenID

            if (pParametros_adiccionales.Contains("proyectoOrigenID="))
            {
                string trozo1 = pParametros_adiccionales.Substring(0, pParametros_adiccionales.IndexOf("proyectoOrigenID="));
                string trozoProyOrgien = pParametros_adiccionales.Substring(pParametros_adiccionales.IndexOf("proyectoOrigenID="));
                string trozo2 = trozoProyOrgien.Substring(trozoProyOrgien.IndexOf("|") + 1);
                trozoProyOrgien = trozoProyOrgien.Substring(0, trozoProyOrgien.IndexOf("|"));

                mProyectoOrigenID = new Guid(trozoProyOrgien.Substring(trozoProyOrgien.IndexOf("=") + 1));
                pParametros_adiccionales = trozo1 + trozo2;
            }

            #endregion

            #region ProyectoVirtualID

            mProyectoVirtualID = pProyectoID;

            if (pParametros_adiccionales.Contains("proyectoVirtualID="))
            {
                string trozo1 = pParametros_adiccionales.Substring(0, pParametros_adiccionales.IndexOf("proyectoVirtualID="));
                string trozoProyOrgien = pParametros_adiccionales.Substring(pParametros_adiccionales.IndexOf("proyectoVirtualID="));
                string trozo2 = trozoProyOrgien.Substring(trozoProyOrgien.IndexOf("|") + 1);
                trozoProyOrgien = trozoProyOrgien.Substring(0, trozoProyOrgien.IndexOf("|"));

                mProyectoVirtualID = new Guid(trozoProyOrgien.Substring(trozoProyOrgien.IndexOf("=") + 1));
                pParametros_adiccionales = trozo1 + trozo2;
            }

            #endregion

            #region Busqueda tipo mapa

            if (!string.IsNullOrEmpty(pParametros_adiccionales) && pParametros_adiccionales.Contains("busquedaTipoMapa=true"))
            {
                mBusquedaTipoMapa = true;
                pParametros_adiccionales = pParametros_adiccionales.Replace("busquedaTipoMapa=true", "");
            }

            #endregion

            #region Facetas para Formularios Semánticos

            if (!string.IsNullOrEmpty(pParametros_adiccionales) && pParametros_adiccionales.Contains("factFormSem=true"))
            {
                mFacetasEnFormSem = true;
                pParametros_adiccionales = pParametros_adiccionales.Replace("factFormSem=true", "");
            }

            #endregion

            //Leo los parámetros de la búsqueda
            mEsMyGnoss = pProyectoID == ProyectoAD.MetaProyecto;
            mEstaEnProyecto = pEstaEnProyecto;
            mEsUsuarioInvitado = pEsUsuarioInvitado;
            mLanguageCode = pLanguageCode;
            try
            {
                mIdentidadID = pIdentidadID;
            }
            catch (Exception)
            {
                mIdentidadID = UsuarioAD.Invitado;
            }
            mAdministradorQuiereVerTodasLasPersonas = pAdministradorVeTodasPersonas;

            EstablecerOrganizacionIDDeProyectoID(mProyectoID);

            mTipoBusqueda = pTipoBusqueda;
            mGrafoID = pGrafo;
            mListaFiltrosFacetasUsuario = new Dictionary<string, List<string>>();
            mFaceta = pFaceta;
            mNumeroFacetas = pNumeroFacetas;
            mParametros_adiccionales = pParametros_adiccionales;

            if (!mIdentidadID.Equals(UsuarioAD.Invitado))
            {
                CargarIdentidad(mIdentidadID);

                if (!IdentidadActual.PerfilID.Equals(null))
                {
                    mPerfilIdentidadID = IdentidadActual.PerfilID;
                }
                if (IdentidadActual.OrganizacionID.HasValue)
                {
                    mOrganizacionPerfilID = IdentidadActual.OrganizacionID.Value;
                }
            }
            else
            {
                mIdentidadActual = mControladorBase.ObtenerIdentidadUsuarioInvitado(UtilIdiomas).ListaIdentidades[UsuarioAD.Invitado];
                mPerfilIdentidadID = UsuarioAD.Invitado;

                mEstaEnProyecto = false;
                mEsUsuarioInvitado = true;
                mAdministradorQuiereVerTodasLasPersonas = false;
            }

            //Comprobaciones de seguridad (por si el usuario altera los parámetro de la petición)
            TipoProyecto tipoProyecto = TipoProyecto.MetaComunidad;

            if (mProyectoID != ProyectoAD.MetaProyecto)
            {
                tipoProyecto = (TipoProyecto)FilaProyecto.TipoProyecto;
            }



            if (!string.IsNullOrEmpty(pFiltroContexto))
            {
                if (pFiltroContexto.StartsWith("\"") && pFiltroContexto.EndsWith("\""))
                {
                    pFiltroContexto = pFiltroContexto.Substring(1, pFiltroContexto.Length - 2);
                }
                #region Configuracion contexto
                //0.- nombreFiltro
                //1.- filtroContextoSelect
                //2.- filtroContextoWhere
                //3.- filtrosOrdenes   

                mFiltroContextoNombre = pFiltroContexto.Split(new string[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)[0];

                mFiltroContextoSelect = pFiltroContexto.Split(new string[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)[1];

                mFiltroContextoWhere = pFiltroContexto.Split(new string[] { "|||" }, StringSplitOptions.RemoveEmptyEntries)[2];

                #endregion
            }

            if (mFaceta != null && mTipoBusqueda == TipoBusqueda.Contactos)
            {
                mCargarArbolCategorias = false;
            }
            if (mFaceta != null && mFaceta.EndsWith("_Lista"))
            {
                //Cargar la faceta categorías en una lista
                mCargarArbolCategorias = false;
                mFaceta = mFaceta.Replace("_Lista", "");
            }
            else if (mFaceta != null && mFaceta.EndsWith("_Arbol"))
            {
                //Cargar la faceta categorías en un árbol
                mCargarArbolCategorias = true;
                mFaceta = mFaceta.Replace("_Arbol", "");
            }
            if (pUbicacionBusqueda.Contains("homeCatalogo"))
            {
                //Cargar las facetas de la home de un catálogo
                pUbicacionBusqueda = pUbicacionBusqueda.Replace("homeCatalogo", "");
                mFacetasHomeCatalogo = true;

                mEsProyectoCatalogo = tipoProyecto.Equals(TipoProyecto.Catalogo) || tipoProyecto.Equals(TipoProyecto.CatalogoNoSocial) || tipoProyecto.Equals(TipoProyecto.CatalogoNoSocialConUnTipoDeRecurso);

                //mCargarArbolCategorias = !mEsProyectoCatalogo;
            }

            //Extraigo los parámetros
            mListaItemsBusqueda = new List<string>();
            mListaFiltros = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> parametrosNegados = null;

            if (!string.IsNullOrEmpty(pParametros))
            {
                string parametrosSinNegados;
                parametrosNegados = UtilServiciosFacetas.ExtraerParametrosNegados(pParametros, out parametrosSinNegados);
                pParametros = parametrosSinNegados;

                string filtroAplicado = ObtenerFiltroAplicadoSinCondicional(pParametros, pParametros_adiccionales);

                mUtilServiciosFacetas.ExtraerParametros(GestorFacetas.FacetasDW, mProyectoID, filtroAplicado, mListaItemsBusqueda, mListaFiltros, mListaFiltrosFacetasUsuario, pIdentidadID);

                // Si alguno de los filtros aplciados es un filtro condicionado, añadir el filtro condicionado a la lista de filtros
                AgregarFiltrosCondicionanFiltroAplicado(filtroAplicado, pParametros_adiccionales, mListaFiltros);
            }

            #region FiltroIdiomaActual
            if (ParametroProyecto.ContainsKey(ParametroAD.PropiedadContenidoMultiIdioma))
            {
                List<string> listaIdiomas = new List<string>();
                listaIdiomas.Add(pLanguageCode);
                mListaFiltros.Add(ParametroProyecto[ParametroAD.PropiedadContenidoMultiIdioma], listaIdiomas);
            }
            #endregion

            if (GruposPorTipo)
            {
                AjustarFiltrosParaFacetasAgrupadas(ref pParametros);
            }

            //Si no viene ningun filtro, obtenemos la primera pagina
            mPrimeraCarga = (mListaFiltrosFacetasUsuario.Count == 0) && string.IsNullOrEmpty(pFiltroContexto) && (parametrosNegados == null || parametrosNegados.Count == 0);

            #region Eliminamos de parametros adicionales los parametros q vengan en la búsqueda
            char[] separador = { '|' };
            string[] args = pParametros.Split(separador, StringSplitOptions.RemoveEmptyEntries);
            char[] separadores = { '=' };

            List<string> filtros = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.IsNullOrEmpty(args[i]))
                {
                    string[] filtro = args[i].Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                    string key = filtro[0];
                    if (!filtros.Contains(key))
                    {
                        filtros.Add(key);
                    }
                }
            }

            if (!pParametros_adiccionales.StartsWith("SPARQL"))
            {
                string[] argsAdicionales = pParametros_adiccionales.Split(separador, StringSplitOptions.RemoveEmptyEntries);
                pParametros_adiccionales = "";
                for (int i = 0; i < argsAdicionales.Length; i++)
                {
                    if (!string.IsNullOrEmpty(argsAdicionales[i]))
                    {
                        if (argsAdicionales[i].StartsWith("(") && argsAdicionales[i].Contains("@"))
                        {
                            // Quitamos los paréntesis
                            string tempFiltro = argsAdicionales[i].Substring(1);
                            tempFiltro = tempFiltro.Substring(0, tempFiltro.Length - 1);

                            string[] delimiter = { "@" };
                            string[] filtrosCondicionados = tempFiltro.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

                            // Solo revisar si se ha aplicado el primer filtro para añadir el resto.
                            string filtro = filtrosCondicionados[0];
                            string[] filtroTroceado = filtro.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                            string key = filtroTroceado[0];
                            string valor = filtroTroceado[1];

                            if (!filtros.Contains(key))
                            {
                                if (pParametros_adiccionales != "")
                                {
                                    pParametros_adiccionales += "|";
                                }

                                pParametros_adiccionales += argsAdicionales[i];
                            }
                        }
                        else
                        {
                            string[] filtro = argsAdicionales[i].Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                            string key = filtro[0];
                            string valor = filtro[1];
                            if (!filtros.Contains(key))
                            {
                                if (pParametros_adiccionales != "")
                                {
                                    pParametros_adiccionales += "|";
                                }
                                pParametros_adiccionales += key + "=" + valor;
                            }
                        }
                    }
                }
            }


            #endregion

            if (!string.IsNullOrEmpty(pParametros_adiccionales))
            {
                mUtilServiciosFacetas.ExtraerParametros(GestorFacetas.FacetasDW, mProyectoID, pParametros_adiccionales, mListaItemsBusqueda, mListaFiltros, mListaFiltros, pIdentidadID);
            }

            bool esBusquedaGrafoHome = EsBusquedaMyGnossGrafoHome(mTipoBusqueda);

            //Selecciono el tipo de elementos que está buscando el usuario
            if (!mListaFiltros.ContainsKey("rdf:type") || mTipoBusqueda.Equals(TipoBusqueda.Recursos))
            {
                ObtenerItemsBusquedaDeTipoBusqueda(mTipoBusqueda, mListaItemsBusqueda);
            }

            mListaItemsBusquedaExtra = mUtilServiciosFacetas.ObtenerListaItemsBusquedaExtra(mListaFiltros, mTipoBusqueda, mOrganizacionID, mProyectoID);


            mFormulariosSemanticos = mUtilServiciosFacetas.ObtenerFormulariosSemanticos(mTipoBusqueda, mOrganizacionID, mProyectoID);

            if (mListaItemsBusqueda.Count > 0 && !mListaFiltros.ContainsKey("rdf:type") && (FilaPestanyaBusquedaActual == null || string.IsNullOrEmpty(FilaPestanyaBusquedaActual.CampoFiltro) || FilaPestanyaBusquedaActual.CampoFiltro.Contains("rdf:type") || !(mListaItemsBusqueda.Count == 1 && mListaItemsBusqueda[0] == "Recurso")))
            {
                mListaFiltros.Add("rdf:type", mListaItemsBusqueda);
            }
            else if (mListaFiltros.ContainsKey("rdf:type"))
            {
                foreach (string filtro in mListaFiltros["rdf:type"])
                {
                    mListaItemsBusqueda.Add(filtro);
                }
            }
            else
            {
                if (mListaItemsBusqueda.Count == 0)
                {
                    mListaFiltros.Add("rdf:type", new List<string>());
                }

                if (mProyectoID.Equals(ProyectoAD.MetaProyecto))
                {
                    mListaItemsBusqueda.Add("Mygnoss");
                }
                else
                {
                    mListaItemsBusqueda.Add("Meta");
                }
            }

            if (pUbicacionBusqueda == "MyGNOSS" + FacetadoAD.BUSQUEDA_AVANZADA)
            {
                pUbicacionBusqueda = FacetadoAD.BUSQUEDA_AVANZADA;
            }

            mUbicacionBusqueda = pUbicacionBusqueda;

            //Preparo el buscador facetado
            Configuracion.ObtenerDesdeFicheroConexion = true;
            mFacetadoDS = new FacetadoDS();

            mFacetadoCL = null;

            string urlGrafo = mUtilServicios.UrlIntragnoss;

            if (mTipoBusqueda == TipoBusqueda.ArticuloBlogs)
            {
                mGrafoID = "blog/" + pGrafo;
            }
            else if (mTipoBusqueda == TipoBusqueda.EditarRecursosPerfil)
            {
                string identidadid = mIdentidadID.ToString();
                if (!string.IsNullOrEmpty(pGrafo))
                {
                    identidadid = pGrafo;
                }
                mGrafoID = "perfil/" + identidadid;
            }
            else if (mTipoBusqueda == TipoBusqueda.VerRecursosPerfil)
            {
                string identidadid = mIdentidadID.ToString();
                if (!string.IsNullOrEmpty(pGrafo))
                {
                    identidadid = pGrafo;
                }
                mGrafoID = "perfil/" + identidadid;
            }
            else if (mTipoBusqueda == TipoBusqueda.Contribuciones)
            {
                if (!string.IsNullOrEmpty(pGrafo))
                {
                    mGrafoID = "contribuciones/" + pGrafo;
                    OrganizacionCN orgCN = new OrganizacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                    bool existeOrg = orgCN.ExisteOrganizacionPorOrganizacionID(pGrafo);

                    IdentidadCN idenCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                    List<Guid> perfilIDOrganizacionID = idenCN.ObtenerPerfilyOrganizacionID(mIdentidadID);
                    idenCN.Dispose();

                    //Si el grafo es de una organizacion y el usuario actual está conectado con esa organización, metemos los filtros para que vea solo sus contribuciones.
                    if ((!mAdministradorQuiereVerTodasLasPersonas) && existeOrg && perfilIDOrganizacionID.Count == 2 && perfilIDOrganizacionID[1].Equals(new Guid(pGrafo)))
                    {
                        //la identidad es de organización
                        List<string> listaAux = new List<string>();

                        listaAux.Add("gnoss:" + pIdentidadID.ToString().ToUpper());
                        mListaFiltros.Add("gnoss:haspublicadorIdentidadID", listaAux);

                        mMostrarFacetaEstado = true;
                        //mListaFiltros.Add("gnoss:hasSpaceIDPublicador", listaAux);
                    }
                }
                else
                {
                    mGrafoID = pGrafo;
                    urlGrafo = mUtilServicios.UrlIntragnoss + "contribuciones/";
                }

                //Comprobación antigua, hay una variable que define si estás en myGnoss o no, trtaía diferentes resultados respecto a la carga normal.
                //Problema en contribuciones de IneveryCrea, bug6958
                //if (!mProyectoID.Equals(ProyectoAD.MetaProyecto))
                if (!mEsMyGnoss)
                {
                    List<string> listaAux = new List<string>();

                    listaAux.Add("gnoss:" + pProyectoID.ToString().ToUpper());
                    mListaFiltros.Add("sioc:has_space", listaAux);
                }
            }
            else if (mTipoBusqueda == TipoBusqueda.Mensajes || mTipoBusqueda == TipoBusqueda.Invitaciones)
            {
                mGrafoID = pGrafo;
                if (pParametros.Equals("enviados") || pParametros.Equals("recibidos") || pParametros.Equals("eliminados"))
                {
                    mPrimeraCarga = true;
                }
                else
                {
                    mPrimeraCarga = false;
                }

                //Meto la identidad actual de búsqueda como filtro para que solo aparezcan mensajes de la misma:
                mListaFiltros.Add("gnoss:IdentidadID", new List<string>());
                mListaFiltros["gnoss:IdentidadID"].Add("gnoss:" + mIdentidadID.ToString().ToUpper());
            }
            else if (mTipoBusqueda == TipoBusqueda.Comentarios || mTipoBusqueda == TipoBusqueda.Suscripciones)
            {
                mGrafoID = pGrafo;
                mPrimeraCarga = false;
            }
            else if (mTipoBusqueda == TipoBusqueda.Notificaciones)
            {
                mGrafoID = pGrafo;
                mPrimeraCarga = false;

                //Meto la identidad actual de búsqueda como filtro para que solo aparezcan mensajes de la misma:
                mListaFiltros.Add("Invitacion;gnoss:IdentidadID", new List<string>());
                mListaFiltros["Invitacion;gnoss:IdentidadID"].Add("gnoss:" + mIdentidadID.ToString().ToUpper());
            }
            else if (mTipoBusqueda == TipoBusqueda.Contactos)
            {
                mGrafoID = "contactos/" + pIdentidadID;
            }
            else if (mTipoBusqueda == TipoBusqueda.Recomendaciones)
            {
                mGrafoID = "contactos/" + pIdentidadID;

                mListaFiltros.Add("gnoss:RecPer", new List<string>());
                mListaFiltros["gnoss:RecPer"].Add("gnoss:" + mIdentidadID.ToString().ToUpper());
                mPrimeraCarga = false;
            }
            else
            {
                mGrafoID = pProyectoID.ToString();
            }

            if (mProyectoOrigenID != Guid.Empty)
            {
                mGrafoID = mProyectoOrigenID.ToString().ToLower();
            }

            if (esBusquedaGrafoHome)
            {
                mFacetadoCL = new FacetadoCL("acidHome", "", urlGrafo, mGrafoID, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
            }
            else
            {
                mFacetadoCL = new FacetadoCL(urlGrafo, mAdministradorQuiereVerTodasLasPersonas, mGrafoID, true, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
            }

            if (mListaFiltros.ContainsKey("rdf:type"))
            {
                GestorFacetas.CargarGestorFacetas(mListaFiltros["rdf:type"]);
            }

            if (mProyectoID != ProyectoAD.MetaProyecto)
            {
                mFacetadoCL.ListaItemsBusquedaExtra = mListaItemsBusquedaExtra;
                mFacetadoCL.InformacionOntologias = InformacionOntologias;
                mFacetadoCL.PropiedadesRango = mUtilServiciosFacetas.ObtenerPropiedadesRango(GestorFacetas);
                mFacetadoCL.PropiedadesFecha = mUtilServiciosFacetas.ObtenerPropiedadesFecha(GestorFacetas);
                mFacetadoCL.ListaComunidadesPrivadasUsuario = new List<Guid>();
            }

            mFacetadoCL.FacetaDW = GestorFacetasOriginal.FacetasDW;

            if (parametrosNegados != null && parametrosNegados.Count > 0)
            {
                List<string> listaItemsBusquedaExtra = mUtilServiciosFacetas.ObtenerListaItemsBusquedaExtra(new Dictionary<string, List<string>>(), mTipoBusqueda, mOrganizacionID, mProyectoID);
                foreach (string facetaNegada in parametrosNegados.Keys)
                {
                    string faceta = facetaNegada.Remove(0, 1);

                    List<string> parametrosAgregarFiltros = new List<string>();

                    //Obtener si es una faceta negada y se quieren obtener los recursos sin ningún valor para esta faceta
                    bool esNothing = parametrosNegados[facetaNegada].Any(item => item.Equals(FacetadoAD.NOTHING));
                    if (esNothing)
                    {
                        parametrosAgregarFiltros.Add(FacetadoAD.NOTHING);
                    }
                    else
                    {
                        FacetadoDS facetadoDS = mFacetadoCL.ObtenerFaceta(faceta, pProyectoID, listaItemsBusquedaExtra, false, false, pIdentidadID.Equals(UsuarioAD.Invitado), pIdentidadID, pEsUsuarioInvitado);
                        //Recorro sus valores y quito los que están negados
                        foreach (DataRow fila in facetadoDS.Tables[0].Rows)
                        {
                            string valor = (string)fila[0];

                            if (!parametrosNegados[facetaNegada].Contains(valor))
                            {
                                parametrosAgregarFiltros.Add(valor);
                            }
                        }
                    }

                    //añado un filtro para los valores que quedan (los que no estaban negados)
                    if (parametrosAgregarFiltros.Count > 0)
                    {
                        mListaFiltros.Add(faceta, parametrosAgregarFiltros);
                    }
                }
            }

            #region Mapa

            if (mBusquedaTipoMapa)
            {
                mFacetadoCL.CondicionExtraFacetas = mFacetadoCL.FacetadoCN.ObtenerFiltroConsultaMapaProyectoDesdeDataSetParaFacetas(mUtilServiciosFacetas.ObtenerDataSetConsultaMapaProyecto(mOrganizacionID, mProyectoID, mTipoBusqueda), mTipoBusqueda, mListaFiltros, InformacionOntologias);
            }

            #endregion


            bool permitirRecursosPrivados = ParametrosGenerales.PermitirRecursosPrivados && !mSinPrivacidad && !(FilaPestanyaBusquedaActual != null && FilaPestanyaBusquedaActual.IgnorarPrivacidadEnBusqueda);

            bool facetaPrivadaGrupo = ObtenerSiAlgunaFacetaEsPrivadaParaGrupoDeEditores(mProyectoID, mPerfilIdentidadID);

            bool tieneRecursosPrivados = false;
            if (mEstaEnProyecto && permitirRecursosPrivados)
            {
                //Si el usuario no tiene 
                tieneRecursosPrivados = UtilServiciosFacetas.ChequearUsuarioTieneRecursosPrivados(facetaPrivadaGrupo, mPerfilIdentidadID, mTipoBusqueda, mProyectoID, mFacetadoCL);
            }

            mFacetadoCL.FacetadoCN.FacetadoAD.UsuarioTieneRecursosPrivados = tieneRecursosPrivados;


            if ((!string.IsNullOrEmpty(mFaceta)) && (pNumeroFacetas == -1))
            {
                if (mListaItemsBusquedaExtra.Count > 0)
                {
                    foreach (string docSem in InformacionOntologias.Keys)
                    {
                        if (!GestorFacetas.OntologiasNoBuscables.Contains(docSem))
                        {
                            mListaItemsBusqueda.Add(docSem);
                        }
                    }
                }

                int numElementosCargar = 30;
                if (mCargarArbolCategorias && mFaceta.Contains("skos:ConceptID"))
                {
                    numElementosCargar = -1;
                }
                else if (mNumElementosFaceta.HasValue)
                {
                    numElementosCargar = mNumElementosFaceta.Value;
                }

                if (GestorFacetas.ListaFacetas.Count > 0)
                {
                    Faceta objetoFaceta = null;

                    //Puede darse el caso de que la faceta skos:conceptid tenga más de un elementos a pintar...
                    if (mFaceta.Contains("skos:ConceptID"))
                    {
                        if (mFaceta.Split(':').Length > 2)
                        {
                            string facetaSinGuid = mFaceta.Substring(0, mFaceta.LastIndexOf(":"));
                            foreach (Faceta facetaSkos in GestorFacetas.ListaFacetas)
                            {
                                if (facetaSkos.ClaveFaceta.Equals(facetaSinGuid) && facetaSkos.FiltroProyectoID.ToLower() == mFaceta.Split(':')[2].ToLower())
                                {
                                    objetoFaceta = facetaSkos;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            foreach (Faceta facetaSkos in GestorFacetas.ListaFacetas)
                            {
                                if (facetaSkos.ClaveFaceta.Equals(mFaceta))
                                {
                                    objetoFaceta = facetaSkos;
                                    break;
                                }
                            }
                        }
                        // TODO: Migrar a EF
                        //}
                        //else if (mFaceta.Split(':').Length > 2 && !GestorFacetas.ListaFacetasPorClave.ContainsKey(mFaceta))
                        //{
                        //    string faceta = mFaceta.Substring(0, mFaceta.LastIndexOf(":"));
                        //    string valorFiltro = mFaceta.Split(':')[2];

                        //    objetoFaceta = GestorFacetas.ListaFacetasPorClave[faceta];
                        //    if (objetoFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Multiple))
                        //    {
                        //        FacetaDS.FacetaMultipleRow[] filasMultiples = ((FacetaDS.FacetaObjetoConocimientoProyectoRow)objetoFaceta.FilaElemento).GetFacetaMultipleRows();
                        //        if (filasMultiples.Length > 0)
                        //        {
                        //            string clausulaFiltro = filasMultiples[0].Filtro;

                        //            if (!mListaFiltros.ContainsKey(clausulaFiltro))
                        //            {
                        //                mListaFiltros.Add(clausulaFiltro, new List<string>());
                        //            }
                        //            mListaFiltros[clausulaFiltro].Add(valorFiltro);
                        //        }
                        //    }
                    }
                    else
                    {
                        objetoFaceta = ObtenerFacetaClaveTipo(mFaceta);
                    }


                    string claveFaceta = objetoFaceta.ClaveFaceta;
                    string nombreFaceta = objetoFaceta.Nombre;
                    TipoDisenio ordenFaceta = objetoFaceta.TipoDisenio;
                    if (numElementosCargar != -1 && !mNumElementosFaceta.HasValue)
                    {
                        if (objetoFaceta.ElementosVisibles > numElementosCargar)
                        {
                            numElementosCargar = objetoFaceta.ElementosVisibles;
                        }
                    }

                    //Obtenemos las facetas excluyentes de esta busqueda...
                    mFacetadoCL.DiccionarioFacetasExcluyentes = ObtenerDiccionarioFacetasExcluyentes();

                    mFacetadoCL.MandatoryRelacion = CalcularMandatoryRelacion();

                    #region Tesauro Semántico

                    string extraContexto = ObtenerExtraContextoTesauroSemantico(objetoFaceta);

                    #endregion

                    if (objetoFaceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Calendario) || objetoFaceta.TipoPropiedad.Equals(TipoPropiedadFaceta.CalendarioConRangos))
                    {
                        ObtenerDeVirtuosoRangoCalendario(objetoFaceta.ClaveFaceta, objetoFaceta, permitirRecursosPrivados, objetoFaceta.Inmutable, pEsMovil);
                    }
                    else if (objetoFaceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Siglo))
                    {
                        ObtenerDeVirtuosoRangoSiglos(objetoFaceta.ClaveFaceta, mListaFiltros, objetoFaceta, true, numElementosCargar, permitirRecursosPrivados, objetoFaceta.Inmutable, pEsMovil);
                    }
                    else if (objetoFaceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Fecha))
                    {
                        ObtenerDeVirtuosoRangoFechas(objetoFaceta.ClaveFaceta, mListaFiltros, objetoFaceta, true, permitirRecursosPrivados, objetoFaceta.Inmutable, pEsMovil);
                    }
                    else if (objetoFaceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Fecha))
                    {
                        if (objetoFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.FechaMinMax))
                        {
                            ObtenerDeVirtuosoRangoMinMax(objetoFaceta.ClaveFaceta, mListaFiltros, objetoFaceta, true, permitirRecursosPrivados, objetoFaceta.Inmutable, pEsMovil);
                        }
                        else
                        {
                            ObtenerDeVirtuosoRangoFechas(objetoFaceta.ClaveFaceta, mListaFiltros, objetoFaceta, true, permitirRecursosPrivados, objetoFaceta.Inmutable, pEsMovil);
                        }
                    }
                    else if (objetoFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Rangos))
                    {
                        ObtenerDeVirtuosoRangoValores(objetoFaceta.ClaveFaceta, mListaFiltros, objetoFaceta, true, numElementosCargar, permitirRecursosPrivados, objetoFaceta.Inmutable, pEsMovil);
                    }
                    else if (objetoFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Multiple))
                    {
                        ObtenerDeVirtuosoFacetaMultiple(objetoFaceta.ClaveFaceta, mListaFiltros, objetoFaceta, true, numElementosCargar, permitirRecursosPrivados, objetoFaceta.Inmutable, pEsMovil);
                    }
                    else
                    {
                        //Obtengo de virtuoso la faceta
                        mFacetadoCL.ObtenerFaceta(mGrafoID, mFacetadoDS, claveFaceta, mListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), ordenFaceta, 0, numElementosCargar, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, false, null, objetoFaceta.Excluyente, false, false, permitirRecursosPrivados, true, objetoFaceta.Reciproca, objetoFaceta.TipoPropiedad, FiltrosSearchPersonalizados, objetoFaceta.Inmutable, pEsMovil);
                    }

                    #region Tesauro Semántico

                    if (!string.IsNullOrEmpty(extraContexto))
                    {
                        mFiltroContextoWhere = mFiltroContextoWhere.Replace(extraContexto, "");

                        if (mFiltroContextoWhere == "")
                        {
                            mFiltroContextoWhere = null;
                        }
                    }

                    #endregion

                    if (mCargarArbolCategorias && mFaceta.Contains("skos:ConceptID"))
                    {
                        objetoFaceta.AlgoritmoTransformacion = TiposAlgoritmoTransformacion.CategoriaArbol;
                    }

                    int limiteOriginal = numElementosCargar;
                    if (numElementosCargar != -1)
                    {
                        limiteOriginal = objetoFaceta.ElementosVisibles; //limite original si pasas -1 pinta todos
                    }

                    //Y solo se muestra si hay más de un elemento
                    mNecesarioMostarTiposElementos = !((mFacetadoDS.Tables.Contains("rdf:type") && mFacetadoDS.Tables["rdf:type"].Rows.Count == 1) && (!mListaFiltrosFacetasUsuario.ContainsKey("rdf:type") || mListaFiltrosFacetasUsuario["rdf:type"].Count == 0)) || mFaceta == "rdf:type";

                    //Carga la Faceta
                    CargarFacetaDinamica(numElementosCargar, limiteOriginal, objetoFaceta, listaFacetas);
                }
            }
            else
            {
                FacetadoCL facetadoCL = new FacetadoCL(mUtilServicios.UrlIntragnoss, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);

                if (esBusquedaGrafoHome)
                {
                    if (mTipoBusqueda.Equals(TipoBusqueda.Mensajes) && !string.IsNullOrEmpty(pParametros))
                    {
                        //facetadoCL = new FacetadoCL("acid", false, "bandeja", UtilServicios.UrlIntragnoss);
                        facetadoCL = new FacetadoCL("acidHome", "bandeja", mUtilServicios.UrlIntragnoss, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    }
                    else
                    {
                        facetadoCL = new FacetadoCL("acidHome", "", mUtilServicios.UrlIntragnoss, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    }
                }
                //si no se está refrescando la caché y no es un contexto
                bool traerDeCache = !mAdministradorQuiereVerTodasLasPersonas && !mEsRefrescoCache && !mSinCache && string.IsNullOrEmpty(mFiltroContextoWhere) && !string.IsNullOrEmpty(mUrlPagina) && !facetaPrivadaGrupo;

                //si es la primera carga
                bool primeraCarga = (!mProyectoID.Equals(ProyectoAD.MetaProyecto) || (mTipoBusqueda != TipoBusqueda.PersonasYOrganizaciones || !mAdministradorQuiereVerTodasLasPersonas));

                // si no es el administrador viendo las personas de mygnoss
                bool personasMyGnoss = mTipoBusqueda != TipoBusqueda.Contactos && mTipoBusqueda != TipoBusqueda.Contribuciones && mTipoBusqueda != TipoBusqueda.EditarRecursosPerfil && mTipoBusqueda != TipoBusqueda.RecomendacionesProys && mTipoBusqueda != TipoBusqueda.VerRecursosPerfil;

                if (traerDeCache && (((mPrimeraCarga) && primeraCarga && personasMyGnoss) || (mFacetasHomeCatalogo && mFaceta == null)))
                {
                    Guid proyectoCache = mProyectoID;
                    if (!mProyectoOrigenID.Equals(Guid.Empty))
                    {
                        proyectoCache = mProyectoOrigenID;
                    }

                    string esBot = "";

                    if (mEsBot)
                    {
                        esBot = "esbot";
                    }

                    if (pParametros_adiccionales != "")
                    {
                        listaFacetas = facetadoCL.ObtenerModeloFacetasDeBusquedaEnProyecto(proyectoCache, FacetadoAD.TipoBusquedaToString(mTipoBusqueda) + pParametros_adiccionales + esBot, mPerfilIdentidadID, pNumeroFacetas, pEsUsuarioInvitado, mLanguageCode, mFacetasHomeCatalogo, mFaceta, mOrganizacionPerfilID, mBusquedaTipoMapa, pParametros, facetaPrivadaGrupo, pEsMovil);
                    }
                    else
                    {
                        listaFacetas = facetadoCL.ObtenerModeloFacetasDeBusquedaEnProyecto(proyectoCache, FacetadoAD.TipoBusquedaToString(mTipoBusqueda) + esBot, mPerfilIdentidadID, pNumeroFacetas, pEsUsuarioInvitado, mLanguageCode, mFacetasHomeCatalogo, mFaceta, mOrganizacionPerfilID, mBusquedaTipoMapa, pParametros, facetaPrivadaGrupo, pEsMovil);
                    }
                }
                else
                {
                    listaFacetas = null;
                }

                if (mTipoBusqueda == TipoBusqueda.Contactos && mEsUsuarioInvitado)
                {
                    return null;
                }

                if (listaFacetas == null)
                {
                    //Obtenemos las facetas excluyentes de esta busqueda...
                    mFacetadoCL.DiccionarioFacetasExcluyentes = ObtenerDiccionarioFacetasExcluyentes();
                    mFacetadoCL.MandatoryRelacion = CalcularMandatoryRelacion();

                    KeyValuePair<List<FacetModel>, List<FacetItemModel>> ListaFacetasYFiltros = CrearYCargarFacetas(mListaItemsBusqueda, pUbicacionBusqueda, pNumeroFacetas, mListaFiltros, facetaPrivadaGrupo, permitirRecursosPrivados, pEsMovil);
                    listaFacetas = ListaFacetasYFiltros.Key;
                    listaFiltros = ListaFacetasYFiltros.Value;

                    if (parametrosNegados != null && parametrosNegados.Count > 0)
                    {
                        foreach (string faceta in parametrosNegados.Keys)
                        {
                            string facetaSinNegar = faceta.Remove(0, 1);
                            FacetModel modeloFaceta = listaFacetas.FirstOrDefault(item => item.FacetKey.Equals(facetaSinNegar));
                            int indiceFiltro = 0;

                            foreach (string item in parametrosNegados[faceta])
                            {
                                FacetItemModel filtro = new FacetItemModel();
                                if (item.Contains(FacetadoAD.NOTHING))
                                {
                                    filtro = new FacetItemModel() { Tittle = "Ninguno", Name = $"{faceta}={item}", Selected = true, Filter = "", Number = -1, FacetItemlist = new List<FacetItemModel>() };
                                }
                                else
                                {
                                    filtro = new FacetItemModel() { Tittle = item, Name = $"{faceta}={item}", Selected = true, Filter = "", Number = -1, FacetItemlist = new List<FacetItemModel>() };
                                }
                                listaFiltros.Add(filtro);

                                if (modeloFaceta != null)
                                {
                                    modeloFaceta.FacetItemList.RemoveAll(elementoFaceta => elementoFaceta.Name.Equals($"{facetaSinNegar}={item}"));

                                    modeloFaceta.FacetItemList.Insert(indiceFiltro++, filtro);
                                }
                            }
                        }
                    }

                    //Si el usuario está en MyGnoss, en la búsqueda de personas y organizaciones (o perfil personal o contribuciones) y tiene permiso para ver todas las personas, no usa la caché
                    if (string.IsNullOrEmpty(mFiltroContextoWhere) && (((mPrimeraCarga) && (!mProyectoID.Equals(ProyectoAD.MetaProyecto) || (mTipoBusqueda != TipoBusqueda.PersonasYOrganizaciones || !mAdministradorQuiereVerTodasLasPersonas)) && mTipoBusqueda != TipoBusqueda.Contribuciones && mTipoBusqueda != TipoBusqueda.EditarRecursosPerfil && mTipoBusqueda != TipoBusqueda.VerRecursosPerfil) || (mFacetasHomeCatalogo)) && !facetaPrivadaGrupo)
                    {
                        Guid proyectoCache = mProyectoID;
                        if (!mProyectoOrigenID.Equals(Guid.Empty))
                        {
                            proyectoCache = mProyectoOrigenID;
                        }

                        string esBot = "";

                        if (mEsBot)
                        {
                            esBot = "esbot";
                        }

                        if (pParametros_adiccionales != "")
                        {
                            facetadoCL.AgregarModeloFacetasDeBusquedaEnProyectoACache(listaFacetas, FacetadoAD.TipoBusquedaToString(mTipoBusqueda) + pParametros_adiccionales + esBot, mPerfilIdentidadID, proyectoCache, pNumeroFacetas, pEsUsuarioInvitado, mLanguageCode, mFacetasHomeCatalogo, mFaceta, mOrganizacionPerfilID, mBusquedaTipoMapa, pParametros, pEsMovil);
                        }
                        else
                        {
                            if (mTipoBusqueda.Equals(TipoBusqueda.Mensajes))
                            {
                                facetadoCL.AgregarModeloFacetasDeBusquedaEnProyectoACache(listaFacetas, FacetadoAD.TipoBusquedaToString(mTipoBusqueda) + esBot, mPerfilIdentidadID, proyectoCache, pNumeroFacetas, pEsUsuarioInvitado, mLanguageCode, mFacetasHomeCatalogo, mFaceta, mOrganizacionPerfilID, mBusquedaTipoMapa, pParametros, 86400, facetaPrivadaGrupo, pEsMovil);
                            }
                            else
                            {
                                facetadoCL.AgregarModeloFacetasDeBusquedaEnProyectoACache(listaFacetas, FacetadoAD.TipoBusquedaToString(mTipoBusqueda) + esBot, mPerfilIdentidadID, proyectoCache, pNumeroFacetas, pEsUsuarioInvitado, mLanguageCode, mFacetasHomeCatalogo, mFaceta, mOrganizacionPerfilID, mBusquedaTipoMapa, pParametros, 0.0, facetaPrivadaGrupo, pEsMovil);
                            }
                        }
                    }
                }
            }

            if (mFacetadoDS != null)
            {
                mFacetadoDS.Dispose();
                mFacetadoDS = null;
            }
            if (mFacetadoCL != null)
            {
                mFacetadoCL.Dispose();
                mFacetadoCL = null;
            }
            if (mGestorTesauro != null)
            {
                mGestorTesauro.Dispose();
                mGestorTesauro = null;
            }
            if (mNivelesCertificacionDW != null)
            {
                mNivelesCertificacionDW = null;
            }
            if (mFilaProyecto != null)
            {
                //mFilaProyecto.Table.DataSet.Dispose();
                mFilaProyecto = null;
            }
            mLoggingService.AgregarEntrada("Acaba CargarFacetas");
            GuardarTraza();

            FacetedModel resultadoFacetadoModel = new FacetedModel();
            resultadoFacetadoModel.FacetList = listaFacetas;
            resultadoFacetadoModel.FilterList = listaFiltros;

            return resultadoFacetadoModel;
        }

        private string CalcularMandatoryRelacion()
        {

            if (FilaPestanyaBusquedaActual != null && FilaPestanyaBusquedaActual.RelacionMandatory != null)
            {
                return FilaPestanyaBusquedaActual.RelacionMandatory;
            }
            else
            {
                return "";
            }
        }

        private Faceta ObtenerFacetaClaveTipo(string pClaveFaceta)
        {
            string tipo = "rdf:type";
            List<string> listaRdfType = new List<string>();
            if (mListaFiltros.ContainsKey(tipo))
            {
                listaRdfType = mListaFiltros[tipo].Select(item => item.ToLower()).ToList();
            }

            if (GestorFacetas.ListaFacetas.Any(item => item.ClaveFaceta.Equals(pClaveFaceta) && listaRdfType.Contains(item.ObjetoConocimiento.ToLower())))
            {
                return GestorFacetas.ListaFacetas.Where(item => item.ClaveFaceta.Equals(pClaveFaceta) && listaRdfType.Contains(item.ObjetoConocimiento.ToLower())).FirstOrDefault();
            }

            return GestorFacetas.ListaFacetasPorClave[pClaveFaceta];
        }

        [NonAction]
        private void AgregarFiltrosCondicionanFiltroAplicado(string pFiltroAplicado, string pParametros_adiccionales, Dictionary<string, List<string>> pListaFiltros)
        {
            Dictionary<string, List<string>> listaFiltrosTemp = new Dictionary<string, List<string>>();

            if (pParametros_adiccionales.Contains("(") && pParametros_adiccionales.Contains("@"))
            {
                foreach (string filtro in pParametros_adiccionales.Split('|'))
                {
                    if (!string.IsNullOrEmpty(filtro) && filtro.StartsWith("("))
                    {
                        // Obtenermos el parametro condicionado
                        string parametroCondicionado = filtro.Substring(filtro.IndexOf("(") + 1);
                        parametroCondicionado = parametroCondicionado.Substring(0, parametroCondicionado.IndexOf(")"));

                        foreach (string filtroAplicado in pListaFiltros.Keys)
                        {
                            foreach (string valorAplicado in pListaFiltros[filtroAplicado])
                            {
                                string filtroCompletoAplicado = $"{filtroAplicado}={valorAplicado}";
                                if (parametroCondicionado.StartsWith(filtroCompletoAplicado))
                                {
                                    string condiciones = parametroCondicionado.Substring(parametroCondicionado.IndexOf("@") + 1);
                                    foreach (string condicion in condiciones.Split('@'))
                                    {
                                        if (!string.IsNullOrEmpty(condicion))
                                        {
                                            string[] datosCondicion = condicion.Split('=');

                                            string filtroCondicion = datosCondicion[0];
                                            string valorCondicion = datosCondicion[1];

                                            if (!listaFiltrosTemp.ContainsKey(filtroCondicion))
                                            {
                                                listaFiltrosTemp.Add(filtroCondicion, new List<string>());
                                            }

                                            listaFiltrosTemp[filtroCondicion].Add(valorCondicion);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (string clave in listaFiltrosTemp.Keys)
            {
                if (!pListaFiltros.ContainsKey(clave))
                {
                    pListaFiltros.Add(clave, new List<string>());
                }

                foreach (string valor in listaFiltrosTemp[clave])
                {
                    pListaFiltros[clave].Add(valor);
                }
            }
        }

        [NonAction]
        private string ObtenerFiltroAplicadoSinCondicional(string pParametros, string pParametros_adiccionales)
        {
            string filtroAplicado = pParametros;
            if (pParametros_adiccionales.Contains("(") && pParametros_adiccionales.Contains("@"))
            {
                // Obtenermos el parametro condicionado
                string parametroCondicionado = pParametros_adiccionales.Substring(pParametros_adiccionales.IndexOf("(") + 1);
                parametroCondicionado = parametroCondicionado.Substring(0, parametroCondicionado.IndexOf(")"));


                // Solo recogemos el RDFTYPE afectado
                string[] filtrosCondicionados = filtroAplicado.Split('|');
                string tempFiltroAplicado = string.Empty;
                foreach (string filtro in filtrosCondicionados)
                {
                    if (parametroCondicionado.Contains(filtro))
                    {
                        if (filtro.StartsWith("rdf:type"))
                        {
                            tempFiltroAplicado += filtro + "|";
                        }
                    }
                    else
                    {
                        tempFiltroAplicado += filtro + "|";
                    }
                }

                filtroAplicado = tempFiltroAplicado;
            }

            return filtroAplicado;
        }

        /// <summary>
        /// Devuelve si el perfil pertenece a algún grupo de editores declarado en la tabla FacetaObjetoConocimientoProyecto
        /// </summary>
        /// <param name="pProyectoID">ProyectoID</param>
        /// <param name="pPerfilID">PerfilID</param>
        /// <returns>TRUE: El perfil pertenece a algún grupo de la comunidad y se le permite ver alguna faceta exclusiva para ese grupo, False: No pertenece a ningún grupo de la comunidad que vea facetas exclusivas</returns>
        [NonAction]
        private bool ObtenerSiAlgunaFacetaEsPrivadaParaGrupoDeEditores(Guid pProyectoID, Guid pPerfilID)
        {
            string[] delimiter = { "|" };
            List<string> gruposPrivadosFacetas = new List<string>();
            foreach (FacetaObjetoConocimientoProyecto facProy in GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(item => item.FacetaPrivadaParaGrupoEditores != null && item.FacetaPrivadaParaGrupoEditores != "''"))
            {
                foreach (string nombreCorto in facProy.FacetaPrivadaParaGrupoEditores.Split(delimiter, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!gruposPrivadosFacetas.Contains(nombreCorto))
                    {
                        gruposPrivadosFacetas.Add(nombreCorto);
                    }
                }
            }

            bool perfilConFacetasPrivadas = false;
            if (gruposPrivadosFacetas.Count > 0)
            {
                //Ver si alguna faceta tiene algún grupo asociado
                IdentidadCN identCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                List<Guid> gruposExclusivos = identCN.ObtenerGruposIDPorNombreCorto(gruposPrivadosFacetas);
                //Buscar si la identidad participa en alguno de los grupos
                if (identCN.ParticipaPerfilEnGrupo(pPerfilID, gruposExclusivos))
                {
                    perfilConFacetasPrivadas = true;
                }

                identCN.Dispose();
            }

            //Si el perfil pertence a algún grupo de los asociados a facetas, no cargar facetas.
            return perfilConFacetasPrivadas;
        }

        [NonAction]
        private bool FacetaPrivadaEditores(string pFacetaPrivadaParaGrupoEditores)
        {
            bool facetaPrivadaGrupoEditores = !string.IsNullOrEmpty(pFacetaPrivadaParaGrupoEditores);
            if (!string.IsNullOrEmpty(pFacetaPrivadaParaGrupoEditores))
            {
                //Si la identidad pertenece al grupo de editores al que se le permite ver la faceta se pintará, sino no.
                string[] delimiter = { "|" };
                IdentidadCN identCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                List<string> nombresCortosGrupos = new List<string>(pFacetaPrivadaParaGrupoEditores.Split(delimiter, StringSplitOptions.RemoveEmptyEntries));

                List<Guid> gruposExclusivos = identCN.ObtenerGruposIDPorNombreCorto(nombresCortosGrupos);

                //Buscar si la identidad participa en alguno de los grupos
                if (identCN.ParticipaPerfilEnGrupo(mIdentidadActual.PerfilID, gruposExclusivos))
                {
                    facetaPrivadaGrupoEditores = false;
                }

                identCN.Dispose();
            }

            return facetaPrivadaGrupoEditores;
        }

        [NonAction]
        private bool EsBusquedaMyGnossGrafoHome(TipoBusqueda pTipoBusqueda)
        {
            return pTipoBusqueda.Equals(TipoBusqueda.Mensajes) || pTipoBusqueda.Equals(TipoBusqueda.Comentarios) || pTipoBusqueda.Equals(TipoBusqueda.Invitaciones) || pTipoBusqueda.Equals(TipoBusqueda.Suscripciones) || pTipoBusqueda.Equals(TipoBusqueda.Notificaciones);
        }

        #endregion

        #region Metodos generales
        [NonAction]
        private void CargarPersonalizacion(Guid pProyectoID)
        {
            CommunityModel comunidad = new CommunityModel();
            comunidad.ListaPersonalizaciones = new List<string>();
            comunidad.ListaPersonalizacionesEcosistema = new List<string>();

            ViewBag.Comunidad = comunidad;

            string controllerName = this.ToString();
            controllerName = controllerName.Substring(controllerName.LastIndexOf('.') + 1);
            controllerName = controllerName.Substring(0, controllerName.IndexOf("Controller"));
            ViewBag.ControllerName = controllerName;

            ViewBag.BaseUrlContent = BaseURLContent;
            ViewBag.BaseUrlPersonalizacion = BaseURLPersonalizacion;

            ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            // las personalizaciones no se cargan en la página de administración de miembros
            if (!mAdministradorQuiereVerTodasLasPersonas || !mTipoBusqueda.Equals(TipoBusqueda.PersonasYOrganizaciones) || !proyCN.EsIdentidadAdministradorProyecto(mIdentidadID, pProyectoID, TipoRolUsuario.Administrador))
            {
                VistaVirtualCL vistaVirtualCL = new VistaVirtualCL(mEntityContext, mLoggingService, mGnossCache, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
                DataWrapperVistaVirtual vistaVirtualDW = vistaVirtualCL.ObtenerVistasVirtualPorProyectoID(pProyectoID, PersonalizacionEcosistemaID, ComunidadExcluidaPersonalizacionEcosistema);

                Guid personalizacionProyecto = Guid.Empty;
                if (vistaVirtualDW.ListaVistaVirtualProyecto.Count > 0)
                {
                    personalizacionProyecto = vistaVirtualDW.ListaVistaVirtualProyecto.First().PersonalizacionID;
                }

                if (mUtilServicios.ComprobacionInvalidarVistasLocales(personalizacionProyecto, PersonalizacionEcosistemaID))
                {
                    vistaVirtualCL.InvalidarVistasVirtualesEnCacheLocal(pProyectoID);
                    vistaVirtualCL.InvalidarVistasVirtualesEcosistemaEnCacheLocal();

                    //Se ha invalidado la caché de vistas, cargo de nuevo el data set de vistas virtuales
                    vistaVirtualDW = vistaVirtualCL.ObtenerVistasVirtualPorProyectoID(pProyectoID, PersonalizacionEcosistemaID, ComunidadExcluidaPersonalizacionEcosistema);

                    mLoggingService.AgregarEntrada("Borramos las vistas del VirtualPathProvider");

                    BDVirtualPath.LimpiarListasRutasVirtuales();
                }

                if (mUtilServicios.ComprobacionInvalidarTraduccionesLocales(personalizacionProyecto, mControladorBase.PersonalizacionEcosistemaID))
                {
                    mUtilIdiomas.CargarTextosPersonalizadosDominio("", mControladorBase.PersonalizacionEcosistemaID);
                }

                comunidad.PersonalizacionProyectoID = personalizacionProyecto;

                if (personalizacionProyecto != Guid.Empty)
                {
                    foreach (Es.Riam.Gnoss.AD.EntityModel.Models.VistaVirtualDS.VistaVirtual filaVistaVirtual in vistaVirtualDW.ListaVistaVirtual.Where(vistaVirtual => vistaVirtual.PersonalizacionID.Equals(personalizacionProyecto)).ToList())
                    {
                        comunidad.ListaPersonalizaciones.Add(filaVistaVirtual.TipoPagina);
                    }

                    ViewBag.Personalizacion = $"$$${personalizacionProyecto}";
                }
                if (PersonalizacionEcosistemaID != Guid.Empty)
                {
                    foreach (Es.Riam.Gnoss.AD.EntityModel.Models.VistaVirtualDS.VistaVirtual filaVistaVirtual in vistaVirtualDW.ListaVistaVirtual.Where(vistaVirtual => vistaVirtual.PersonalizacionID.Equals(PersonalizacionEcosistemaID)).ToList())
                    {
                        comunidad.ListaPersonalizacionesEcosistema.Add(filaVistaVirtual.TipoPagina);
                    }

                    ViewBag.PersonalizacionEcosistema = $"$$${PersonalizacionEcosistemaID}";
                }
            }

        }
        [NonAction]
        public void ObtenerItemsBusquedaDeTipoBusqueda(TipoBusqueda pTipoBusqueda, List<string> pListaItemsBusqueda)
        {
            switch (pTipoBusqueda)
            {
                case TipoBusqueda.Recursos:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_RECURSOS);
                    break;
                case TipoBusqueda.Debates:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_DEBATES);
                    break;
                case TipoBusqueda.Preguntas:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_PREGUNTAS);
                    break;
                case TipoBusqueda.Encuestas:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_ENCUESTAS);
                    break;
                case TipoBusqueda.Dafos:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_DAFOS);
                    break;
                case TipoBusqueda.PersonasYOrganizaciones:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_ORGANIZACION);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CLASE);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_PERSONA);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_GRUPO);
                    break;
                case TipoBusqueda.Comunidades:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_COMUNIDADES);
                    break;
                case TipoBusqueda.RecomendacionesProys:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_COMUNIDADES_RECOMENDADAS);
                    break;
                case TipoBusqueda.Blogs:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_BLOGS);
                    break;
                case TipoBusqueda.ArticuloBlogs:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_ARTICULOSBLOG);
                    break;
                case TipoBusqueda.Contribuciones:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_RECURSOS);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PUBLICADO);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPARTIDO);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENTARIOS);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PREGUNTA);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_DEBATE);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_FACTORDAFO);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_ENCUESTA);
                    break;
                case TipoBusqueda.EditarRecursosPerfil:
                case TipoBusqueda.VerRecursosPerfil:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_RECURSOS_PERSONALES);
                    break;
                case TipoBusqueda.Mensajes:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_MENSAJES);
                    break;
                case TipoBusqueda.Comentarios:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_COMENTARIOS);
                    break;
                case TipoBusqueda.Invitaciones:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_INVITACIONES);
                    break;
                case TipoBusqueda.Notificaciones:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_INVITACIONES);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_COMENTARIOS);
                    break;
                case TipoBusqueda.Suscripciones:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_SUSCRIPCIONES);
                    break;
                case TipoBusqueda.Contactos:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTACTOS);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTACTOS_PERSONAL);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTACTOS_ORGANIZACION);
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTACTOS_GRUPO);
                    break;
                case TipoBusqueda.Recomendaciones:
                    pListaItemsBusqueda.Add(FacetadoAD.BUSQUEDA_CONTACTOS_PERSONAL);
                    break;
            }
        }

        /// <summary>
        /// Carga las facetas desde virtuoso y genera las fichas
        /// </summary>
        /// <param name="pListaItemsBusqueda">Lista de tipos de resultados que se van a buscar</param>
        /// <param name="pTipoBusqueda">Tipo de la búsqueda (recursos, personas y organizaciones...)</param>
        /// <param name="pNumeroFacetas">Número de facetas a mostar (-1 para cargar desde la 9 hasta la última, -2 para cargar todas a la vez, para bots)</param>
        /// <param name="pListaFiltros">Lista de filtros que ha introducido el usuario</param>
        [NonAction]
        public KeyValuePair<List<FacetModel>, List<FacetItemModel>> CrearYCargarFacetas(List<string> pListaItemsBusqueda, string pTipoBusqueda, int pNumeroFacetas, Dictionary<string, List<string>> pListaFiltros, bool pFacetaPrivadaGrupo, bool pPermitirRecursosPrivados, bool pEsMovil)
        {
            List<FacetModel> listaFacetasDevolver = new List<FacetModel>();
            List<FacetItemModel> listaFiltrosDevolver = new List<FacetItemModel>();

            bool excluirPersonas = false;
            if ((TipoProyecto)FilaProyecto.TipoProyecto == TipoProyecto.Catalogo && !ParametrosGenerales.MostrarPersonasEnCatalogo)
            {
                excluirPersonas = true;
            }

            List<string> listaItems = new List<string>(pListaItemsBusqueda);
            FacetaCL facetaCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            mFacetadoCL.FacetadoCN.FacetadoAD.UsarMismsaVariablesParaEntidadesEnFacetas = ParametroProyecto.ContainsKey(ParametroAD.UsarMismsaVariablesParaEntidadesEnFacetas) && ParametroProyecto[ParametroAD.UsarMismsaVariablesParaEntidadesEnFacetas].Equals("1");

            if ((!mListaFiltrosFacetasUsuario.ContainsKey("rdf:type")) && (listaItems.Count > 0))
            {
                listaItems.AddRange(mListaItemsBusquedaExtra);
            }
            if ((!listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENTARIOS)) && (
                (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMRECURSOS)) ||
                (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPREGUNTAS)) ||
                (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMDEBATES)) ||
                (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENCUESTAS)) ||
                (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMFACTORDAFO)) ||
                (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMARTICULOBLOG))))
            {
                listaItems.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENTARIOS);
            }

            //Añado los tipos padres a la lista de items
            if ((!listaItems.Contains(FacetadoAD.BUSQUEDA_CLASE)) && ((listaItems.Contains(FacetadoAD.BUSQUEDA_CLASE_SECUNDARIA)) || (listaItems.Contains(FacetadoAD.BUSQUEDA_CLASE_UNIVERSIDAD))))
            {
                listaItems.Add(FacetadoAD.BUSQUEDA_CLASE);
            }

            if ((!listaItems.Contains(FacetadoAD.BUSQUEDA_COMUNIDADES)) && ((listaItems.Contains(FacetadoAD.BUSQUEDA_COMUNIDAD_EDUCATIVA)) || (listaItems.Contains(FacetadoAD.BUSQUEDA_COMUNIDAD_NO_EDUCATIVA))))
            {
                listaItems.Add(FacetadoAD.BUSQUEDA_COMUNIDADES);
            }

            if ((!listaItems.Contains(FacetadoAD.BUSQUEDA_PERSONA)) && (listaItems.Contains(FacetadoAD.BUSQUEDA_PROFESOR) || listaItems.Contains(FacetadoAD.BUSQUEDA_ALUMNO)))
            {
                listaItems.Add(FacetadoAD.BUSQUEDA_PERSONA);
            }

            if ((!listaItems.Contains(FacetadoAD.BUSQUEDA_CONTACTOS)) && (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTACTOS_GRUPO) || listaItems.Contains(FacetadoAD.BUSQUEDA_CONTACTOS_ORGANIZACION) || listaItems.Contains(FacetadoAD.BUSQUEDA_CONTACTOS_PERSONAL)))
            {
                listaItems.Add(FacetadoAD.BUSQUEDA_CONTACTOS);
            }

            bool esBusquedaAvanzadaPrimeraCargaEnMetaProyecto = mTipoBusqueda.Equals(TipoBusqueda.BusquedaAvanzada) && mPrimeraCarga && ProyectoSeleccionado.Clave == ProyectoAD.MetaProyecto;

            List<string> listaTablas = new List<string>();
            bool omitirPalabrasNoRelevantesSearch = true;
            mNecesarioMostarTiposElementos = true;

            Faceta facetaTipo = null;
            if (GestorFacetas.ListaFacetasPorClave.ContainsKey("rdf:type"))
            {
                facetaTipo = GestorFacetas.ListaFacetasPorClave["rdf:type"];
            }

            TipoDisenio tipodisenio = TipoDisenio.ListaMayorAMenor;
            TipoPropiedadFaceta tipoPropiedadFaceta = TipoPropiedadFaceta.NULL;
            bool excluyente = true;
            int reciproca = 0;
            if (facetaTipo != null)
            {
                tipodisenio = facetaTipo.TipoDisenio;
                excluyente = facetaTipo.Excluyente;
                reciproca = facetaTipo.Reciproca;
                tipoPropiedadFaceta = facetaTipo.TipoPropiedad;
            }

            if (esBusquedaAvanzadaPrimeraCargaEnMetaProyecto)
            {
                //Para que cuando se entre sin filtros a la búsqueda avanzada de MyGnoss, la faceta de tipos de elementos aparezca expandida
                facetaTipo.ElementosVisibles = 25;
            }

            bool recursosCargados = false;

            Dictionary<string, List<string>> pListaFiltrosAntiguos = new Dictionary<string, List<string>>(pListaFiltros);

            if (GruposPorTipo)
            {
                pListaFiltros = new Dictionary<string, List<string>>(mListaFiltrosConGrupos);

                //Modificamos los filtros para que no filtre por tipo(para que traiga todos los contadores de los tipos)
                pListaFiltros.Remove("rdf:type");
                pListaFiltros.Remove("default;rdf:type");
                string[] tipos = FilaPestanyaBusquedaActual.CampoFiltro.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                List<string> listaTipos = new List<string>();
                foreach (string tipo in tipos)
                {
                    listaTipos.Add(tipo.Replace("rdf:type=", ""));
                }
                pListaFiltros.Add("rdf:type", listaTipos);
            }

            FacetadoDS facetadoDSComprobacion = mFacetadoDS;

            CargarFiltrosSearchPersonalizados();

            if (mListaFiltros.ContainsKey("search"))
            {
                //Obtengo la faceta explora
                mFacetadoCL.ObtenerFaceta(mGrafoID, mFacetadoDS, "rdf:type", pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), tipodisenio, 0, 25, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, false, null, excluyente, false, excluirPersonas, pPermitirRecursosPrivados, false, reciproca, tipoPropiedadFaceta, FiltrosSearchPersonalizados, false, pEsMovil);

                //Si la búsqueda obtiene resutlados salimos del bucle
                if (mFacetadoDS.Tables.Contains("rdf:type") && mFacetadoDS.Tables["rdf:type"].Rows.Count > 0)
                {
                    recursosCargados = true;
                    omitirPalabrasNoRelevantesSearch = false;
                }

                if (mFacetadoDS.Tables.Contains("rdf:type") && mFacetadoDS.Tables["rdf:type"].Rows.Count == 1)
                {
                    // Obtengo las facetas sin filtros de usuario, para comprobar si tiene que aparecer la faceta rdf:type o no
                    Dictionary<string, List<string>> filtrosPagina = EliminarFiltrosDeListaFiltros(mListaFiltros, mListaFiltrosFacetasUsuario);
                    facetadoDSComprobacion = new FacetadoDS();

                    if (filtrosPagina.ContainsKey("rdf:type"))
                    {
                        facetadoDSComprobacion.Tables.Add("rdf:type");
                        facetadoDSComprobacion.Tables["rdf:type"].Columns.Add("rdftype2000");
                        facetadoDSComprobacion.Tables["rdf:type"].Columns.Add("a");
                        foreach (string tipo in filtrosPagina["rdf:type"])
                        {
                            facetadoDSComprobacion.Tables["rdf:type"].Rows.Add(tipo, "1");
                        }
                    }
                    else
                    {
                        mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSComprobacion, "rdf:type", filtrosPagina, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), tipodisenio, 0, 25, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, false, null, excluyente, false, excluirPersonas, pPermitirRecursosPrivados, false, reciproca, tipoPropiedadFaceta, FiltrosSearchPersonalizados, false, pEsMovil);
                    }
                }
            }

            if (!recursosCargados && pListaFiltros.ContainsKey("rdf:type") && pListaFiltros["rdf:type"].Count == 1)
            {
                //Añadir el tipo rdf:type a la tabla.
                mFacetadoDS.Tables.Add("rdf:type");
                mFacetadoDS.Tables["rdf:type"].Columns.Add("rdftype2", typeof(string));
                mFacetadoDS.Tables["rdf:type"].Columns.Add("a", typeof(string));

                DataRow fila = mFacetadoDS.Tables["rdf:type"].NewRow();
                fila["rdftype2"] = pListaFiltros["rdf:type"][0];
                fila["a"] = "1";
                mFacetadoDS.Tables["rdf:type"].Rows.Add(fila);
            }
            else if (!recursosCargados)
            {
                //Obtengo la faceta explora
                mFacetadoCL.ObtenerFaceta(mGrafoID, mFacetadoDS, "rdf:type", pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), tipodisenio, 0, 25, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, false, null, excluyente, false, excluirPersonas, pPermitirRecursosPrivados, FiltrosSearchPersonalizados, false, pEsMovil);

                if (mFacetadoDS.Tables.Contains("rdf:type") && mFacetadoDS.Tables["rdf:type"].Rows.Count == 1 && mListaFiltrosFacetasUsuario.Count > 0)
                {
                    // Obtengo las facetas sin filtros de usuario, para comprobar si tiene que aparecer la faceta rdf:type o no
                    Dictionary<string, List<string>> filtrosPagina = EliminarFiltrosDeListaFiltros(mListaFiltros, mListaFiltrosFacetasUsuario);
                    facetadoDSComprobacion = new FacetadoDS();
                    if (filtrosPagina.ContainsKey("rdf:type"))
                    {
                        facetadoDSComprobacion.Tables.Add("rdf:type");
                        facetadoDSComprobacion.Tables["rdf:type"].Columns.Add("rdftype2000");
                        facetadoDSComprobacion.Tables["rdf:type"].Columns.Add("a");
                        foreach (string tipo in filtrosPagina["rdf:type"])
                        {
                            facetadoDSComprobacion.Tables["rdf:type"].Rows.Add(tipo, "1");
                        }
                    }
                    else
                    {
                        mFacetadoCL.FacetadoCN.FacetadoAD.ObtenerContadorDeFaceta = false;
                        mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSComprobacion, "rdf:type", filtrosPagina, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), tipodisenio, 0, 25, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, false, null, excluyente, false, excluirPersonas, pPermitirRecursosPrivados, FiltrosSearchPersonalizados, false, pEsMovil);
                        mFacetadoCL.FacetadoCN.FacetadoAD.ObtenerContadorDeFaceta = true;
                    }
                }
            }

            if (GruposPorTipo)
            {
                //Devolvemos a pListaFiltros su valor original
                pListaFiltros = pListaFiltrosAntiguos;
            }

            listaTablas.Add("rdf:type");

            //Y solo se muestra si hay más de un elemento (o estamos agrupando por tipo) 
            mNecesarioMostarTiposElementos = (!((facetadoDSComprobacion.Tables.Contains("rdf:type") && facetadoDSComprobacion.Tables["rdf:type"].Rows.Count == 1) && (!mListaFiltrosFacetasUsuario.ContainsKey("rdf:type") || mListaFiltrosFacetasUsuario["rdf:type"].Count == 0))) || GruposPorTipo;

            //Si no hay ningun elemento no continúa (excepto en mensajes)
            if (!mTipoBusqueda.Equals(TipoBusqueda.Mensajes) && mFacetadoDS.Tables.Contains("rdf:type") && mFacetadoDS.Tables["rdf:type"].Rows.Count == 0)
            {
                return new KeyValuePair<List<FacetModel>, List<FacetItemModel>>(new List<FacetModel>(), new List<FacetItemModel>());
            }

            if (mFacetadoDS.Tables.Contains("rdf:type") && mFacetadoDS.Tables["rdf:type"].Rows.Count == 1)
            {
                //Configuro la búsqueda para que solo tenga un tipo de elemento
                DataRow myrow = mFacetadoDS.Tables["rdf:type"].Rows[0];
                string tipo = (string)myrow[0];

                if ((!tipo.Equals(FacetadoAD.BUSQUEDA_CLASE_SECUNDARIA)) && (!tipo.Equals(FacetadoAD.BUSQUEDA_CLASE_UNIVERSIDAD)) && (!tipo.Equals(FacetadoAD.BUSQUEDA_COMUNIDAD_EDUCATIVA)) && (!tipo.Equals(FacetadoAD.BUSQUEDA_COMUNIDAD_NO_EDUCATIVA)))
                {
                    pTipoBusqueda = "particular";
                    listaItems.Clear();
                    if (InformacionOntologias.Keys.Contains(tipo))
                    {
                        listaItems.Add("Recurso");
                    }
                    listaItems.Add(tipo);
                }
            }

            //Si es la búsqueda avanzada y es la primera carga, sólo se carga la faceta Explora...
            //Si no, se cargan todas las facetas
            if (!esBusquedaAvanzadaPrimeraCargaEnMetaProyecto)
            {
                if ((!listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_RECURSOS)) && ((listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPARTIDO)) || (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PUBLICADO)) || (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_DEBATE)) || (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PREGUNTA)) || (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_ENCUESTA)) || (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_FACTORDAFO))))
                {
                    listaItems.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_RECURSOS);
                }

                if ((!listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENTARIOS)) && (
                    (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMRECURSOS)) ||
                    (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPREGUNTAS)) ||
                    (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMDEBATES)) ||
                    (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENCUESTAS)) ||
                    (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMFACTORDAFO)) ||
                    (listaItems.Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMARTICULOBLOG))))
                {
                    listaItems.Add(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENTARIOS);
                }
            }

            mListaFiltrosFacetasNombreReal = new Dictionary<string, List<string>>();
            foreach (string key in pListaFiltros.Keys)
            {
                mListaFiltrosFacetasNombreReal.Add(key, new List<string>());
                mListaFiltrosFacetasNombreReal[key].AddRange(pListaFiltros[key]);
            }

            #region Ajusto inicio y fin de las facetas

            EliminarFacetasNoAplicables(pNumeroFacetas, pTipoBusqueda, facetadoDSComprobacion);

            List<Faceta> listaFacetasRecorrer = GestorFacetas.ListaFacetas.Where(faceta => !faceta.OcultaEnFacetas).ToList();

            int inicio = 0;
            int fin = pNumeroFacetas;

            bool plegadas = pNumeroFacetas.Equals(3) && mParametroProyecto.ContainsKey(ParametroAD.TerceraPeticionFacetasPlegadas) && mParametroProyecto[ParametroAD.TerceraPeticionFacetasPlegadas].Equals("1");

            if (mParametroProyecto.ContainsKey(ParametroAD.NumeroFacetasPrimeraPeticion) || mParametroProyecto.ContainsKey(ParametroAD.NumeroFacetasSegundaPeticion))
            {
                AjusteInicioFinFacetasConfigurado(pNumeroFacetas, out inicio, out fin, listaFacetasRecorrer);
            }
            else
            {
                AjusteInicioFinFacetas(pNumeroFacetas, out inicio, out fin, ref listaFacetasRecorrer);
            }

            //Juan: Código experimental, para traer una faceta más en las primeras peticiones si el tipo no lo vamos a cargar como faceta.
            if (mFacetadoDS.Tables.Contains("rdf:type") && mFacetadoDS.Tables["rdf:type"].Rows.Count == 0 && listaFacetasRecorrer.Exists(f => f.ClaveFaceta.Equals("rdf:type")))
            {
                int indiceFacetaTipo = listaFacetasRecorrer.IndexOf(listaFacetasRecorrer.First(f => f.ClaveFaceta.Equals("rdf:type")));
                if (fin >= indiceFacetaTipo)
                {
                    if (fin < listaFacetasRecorrer.Count)
                    {
                        fin++;
                    }
                    if (inicio > indiceFacetaTipo)
                    {
                        inicio++;
                    }
                }
            }

            #endregion

            Dictionary<string, int> listaFacetasPlegadas = new Dictionary<string, int>();
            Dictionary<string, string> listaFacetasPlegadasExtraContexto = new Dictionary<string, string>();
            bool usarHilos = ParametrosAplicacionDS.Any(item => item.Parametro.Equals(TiposParametrosAplicacion.UsarHilosEnFacetas) && item.Valor.Equals("1"));
            List<Task> listaTareasFacetas = new List<Task>();

            if (!esBusquedaAvanzadaPrimeraCargaEnMetaProyecto)
            {
                for (int i = inicio; i < fin && i < listaFacetasRecorrer.Count; i++)
                {
                    string extraContexto = "";
                    try
                    {
                        Faceta faceta = listaFacetasRecorrer[i];

                        bool cargarFaceta = !faceta.OcultaEnFacetas;
                        if (cargarFaceta)
                        {
                            bool facetaPrivadaGrupoEditores = false;
                            if (!string.IsNullOrEmpty(faceta.FacetaPrivadaParaGrupoEditores))
                            {
                                facetaPrivadaGrupoEditores = FacetaPrivadaEditores(faceta.FacetaPrivadaParaGrupoEditores);
                            }

                            //Si es una faceta privada para este editor, no pintarla
                            if (!facetaPrivadaGrupoEditores && (mFaceta == null || faceta.ClaveFaceta.Equals(mFaceta)))
                            {
                                if (mCargarArbolCategorias && faceta.ClaveFaceta == "skos:ConceptID")
                                {
                                    faceta.ElementosVisibles = -1;
                                }

                                string algoritmoTransformacion = faceta.AlgoritmoTransformacion.ToString();

                                #region Bandeja de mensajes

                                Dictionary<string, List<string>> listaFiltrosSalvados = null;

                                if (mTipoBusqueda == TipoBusqueda.Mensajes && faceta.ClaveFaceta == "dce:type")
                                {
                                    listaFiltrosSalvados = new Dictionary<string, List<string>>();

                                    foreach (string facetaSalvar in pListaFiltros.Keys)
                                    {
                                        if (facetaSalvar != "gnoss:IdentidadID" && facetaSalvar != "rdf:type")
                                        {
                                            listaFiltrosSalvados.Add(facetaSalvar, pListaFiltros[facetaSalvar]);
                                        }
                                    }

                                    foreach (string facetaQuitar in listaFiltrosSalvados.Keys)
                                    {
                                        pListaFiltros.Remove(facetaQuitar);
                                    }
                                }

                                #endregion

                                extraContexto = ObtenerExtraContextoTesauroSemantico(faceta);

                                // No es un proyecto social con un tipo de recurso Y (Carga si es la primera carga y no se debe mostrar solo la caja O no es la primera carga y no se debe mostrar solo la caja siempre)
                                bool primeraCargaYNoMostrarSoloCaja = !FilaProyecto.TipoProyecto.Equals(TipoProyecto.CatalogoNoSocialConUnTipoDeRecurso) && ((mPrimeraCarga && !faceta.MostrarSoloCaja) || (!mPrimeraCarga && !faceta.MostrarSoloCajaSiempre));

                                // No es una carga de Tags O No es la home de un catalogo O No es un proyecto social con un tipo de recurso
                                bool noPimeraCargaYNoMostrarSoloCaja = faceta.ClaveFaceta != "sioc_t:Tag" || !mFacetasHomeCatalogo || !FilaProyecto.TipoProyecto.Equals(TipoProyecto.CatalogoNoSocialConUnTipoDeRecurso);

                                //Se obtiene de virtuoso el contenido de la faceta
                                if ((primeraCargaYNoMostrarSoloCaja && noPimeraCargaYNoMostrarSoloCaja) || !string.IsNullOrEmpty(mFiltroContextoWhere))
                                {
                                    if (mTipoBusqueda == TipoBusqueda.RecomendacionesProys)
                                    {
                                        mFacetadoDS.Merge(mFacetadoCL.ComunidadesQueTePuedanInteresar(mIdentidadID, 0, faceta.ElementosVisibles * 2, false, mListaFiltros));
                                    }

                                    if (mFacetadoDS.Tables[faceta.ClaveFaceta] == null)
                                    {
                                        List<string> listaItemsBusquedaExtra = mListaItemsBusquedaExtra;
                                        Dictionary<string, List<string>> listaFiltros = pListaFiltros;
                                        if (mFacetasHomeCatalogo)
                                        {
                                            string pestanyaFaceta = "";
                                            if (!string.IsNullOrEmpty(mPestanyaFacetaCMS))
                                            {
                                                pestanyaFaceta = mPestanyaFacetaCMS;
                                            }
                                            if (string.IsNullOrEmpty(pestanyaFaceta) && !string.IsNullOrEmpty(faceta.PestanyaFaceta))
                                            {
                                                pestanyaFaceta = faceta.PestanyaFaceta;
                                            }

                                            if (!string.IsNullOrEmpty(pestanyaFaceta) && string.IsNullOrEmpty(mPestanyaFacetaCMS))
                                            {
                                                listaFiltros = new Dictionary<string, List<string>>();
                                                TipoBusqueda tipoBusqueda = TipoBusqueda.BusquedaAvanzada;

                                                if (pestanyaFaceta.ToLower() == "busqueda")
                                                {
                                                    tipoBusqueda = TipoBusqueda.BusquedaAvanzada;
                                                }
                                                if (pestanyaFaceta.ToLower() == "recursos")
                                                {
                                                    tipoBusqueda = TipoBusqueda.Recursos;
                                                }
                                                else if (pestanyaFaceta.ToLower() == "debates")
                                                {
                                                    tipoBusqueda = TipoBusqueda.Debates;
                                                    listaItemsBusquedaExtra = new List<string>();
                                                }
                                                else if (pestanyaFaceta.ToLower() == "preguntas")
                                                {
                                                    tipoBusqueda = TipoBusqueda.Preguntas;
                                                    listaItemsBusquedaExtra = new List<string>();
                                                }
                                                else if (pestanyaFaceta.ToLower() == "encuestas")
                                                {
                                                    tipoBusqueda = TipoBusqueda.Encuestas;
                                                    listaItemsBusquedaExtra = new List<string>();
                                                }
                                                else if (pestanyaFaceta.ToLower() == "personas-y-organizaciones")
                                                {
                                                    tipoBusqueda = TipoBusqueda.PersonasYOrganizaciones;
                                                    listaItemsBusquedaExtra = new List<string>();
                                                }
                                                else
                                                {
                                                    List<Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.ProyectoPestanyaMenu> filasPestanya = PestanyasProyectoDW.ListaProyectoPestanyaMenu.Where(proy => proy.Ruta.Equals(pestanyaFaceta) && proy.TipoPestanya.Equals((short)TipoPestanyaMenu.BusquedaSemantica)).ToList();

                                                    if (filasPestanya.Count == 1)
                                                    {
                                                        Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.ProyectoPestanyaMenu filaPestanyaMenu = filasPestanya.First();

                                                        if (filaPestanyaMenu.ProyectoPestanyaBusqueda != null)
                                                        {
                                                            tipoBusqueda = TipoBusqueda.Recursos;
                                                            string filtro = filaPestanyaMenu.ProyectoPestanyaBusqueda.CampoFiltro;
                                                            listaItemsBusquedaExtra = new List<string>();
                                                            listaFiltros = new Dictionary<string, List<string>>();
                                                            mUtilServiciosFacetas.ExtraerParametros(GestorFacetas.FacetasDW, mProyectoID, filtro, listaItemsBusquedaExtra, listaFiltros, listaFiltros, IdentidadActual.Clave);
                                                        }
                                                    }
                                                }

                                                listaItemsBusquedaExtra = mUtilServiciosFacetas.ObtenerListaItemsBusquedaExtra(listaFiltros, tipoBusqueda, mOrganizacionID, mProyectoID);

                                                List<string> listaItemsBusqueda = new List<string>();
                                                ObtenerItemsBusquedaDeTipoBusqueda(tipoBusqueda, listaItemsBusqueda);
                                                if (listaItemsBusqueda.Count > 0 && !listaFiltros.ContainsKey("rdf:type"))
                                                {
                                                    listaFiltros.Add("rdf:type", listaItemsBusqueda);
                                                }

                                            }
                                        }

                                        #region SubType

                                        List<string> listaItemsBusquedaExtraBK = null;

                                        if (faceta.ClaveFaceta.EndsWith(FacetaAD.Faceta_Gnoss_SubType) && (faceta.FilaElementoEntity is FacetaFiltroProyecto || faceta.FilaElementoEntity is FacetaFiltroHome) && !string.IsNullOrEmpty(faceta.FiltroProyectoID))
                                        {
                                            if (faceta.FiltroProyectoID.StartsWith("filtroBusquedaAñadir:") || faceta.FiltroProyectoID.StartsWith("filtroBusquedaSustituir:"))
                                            {
                                                string filtroBusqueda = faceta.FiltroProyectoID;
                                                if (faceta.FiltroProyectoID.StartsWith("filtroBusquedaAñadir:"))
                                                {
                                                    filtroBusqueda = filtroBusqueda.Replace("filtroBusquedaAñadir:", "");
                                                }
                                                else
                                                {
                                                    filtroBusqueda = filtroBusqueda.Replace("filtroBusquedaSustituir:", "");
                                                    foreach (string filtro in filtroBusqueda.Split(new string[] { "&" }, StringSplitOptions.RemoveEmptyEntries))
                                                    {
                                                        string clave = filtro.Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries)[0];
                                                        pListaFiltros.Remove(clave);
                                                    }
                                                }

                                                foreach (string filtro in filtroBusqueda.Split(new string[] { "&" }, StringSplitOptions.RemoveEmptyEntries))
                                                {
                                                    string[] claveValor = filtro.Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                                                    string clave = claveValor[0];
                                                    string valor = claveValor[1];
                                                    if (pListaFiltros.ContainsKey(clave))
                                                    {
                                                        pListaFiltros[clave].Add(valor);
                                                    }
                                                    else
                                                    {
                                                        List<string> valores = new List<string>();
                                                        valores.Add(valor);
                                                        pListaFiltros.Add(clave, valores);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                listaItemsBusquedaExtra = new List<string>();
                                                listaItemsBusquedaExtra.AddRange(faceta.FiltroProyectoID.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                                                listaItemsBusquedaExtraBK = mFacetadoCL.ListaItemsBusquedaExtra;
                                                mFacetadoCL.ListaItemsBusquedaExtra = listaItemsBusquedaExtra;
                                            }

                                        }

                                        #endregion

                                        if (plegadas)
                                        {
                                            listaFacetasPlegadas.Add(faceta.ClaveFaceta, faceta.Reciproca);
                                            listaFacetasPlegadasExtraContexto.Add(faceta.ClaveFaceta, extraContexto);
                                        }
                                        else if (faceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Fecha))
                                        {
                                            if (!listaTablas.Contains(faceta.ClaveFaceta))
                                            {
                                                if (faceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.FechaMinMax))
                                                {
                                                    ObtenerDeVirtuosoRangoMinMax(faceta.ClaveFaceta, pListaFiltros, faceta, omitirPalabrasNoRelevantesSearch, pPermitirRecursosPrivados, faceta.Inmutable, pEsMovil);
                                                }
                                                else
                                                {
                                                    ObtenerDeVirtuosoRangoFechas(faceta.ClaveFaceta, pListaFiltros, faceta, omitirPalabrasNoRelevantesSearch, pPermitirRecursosPrivados, faceta.Inmutable, pEsMovil);
                                                }

                                                listaTablas.Add(faceta.ClaveFaceta);
                                            }
                                        }
                                        else if (faceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Siglo))
                                        {
                                            if (!listaTablas.Contains(faceta.ClaveFaceta))
                                            {
                                                ObtenerDeVirtuosoRangoSiglos(faceta.ClaveFaceta, pListaFiltros, faceta, omitirPalabrasNoRelevantesSearch, faceta.ElementosVisibles, pPermitirRecursosPrivados, faceta.Inmutable, pEsMovil);
                                                listaTablas.Add(faceta.ClaveFaceta);
                                            }
                                        }
                                        else if (faceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Rangos))
                                        {
                                            if (!listaTablas.Contains(faceta.ClaveFaceta))
                                            {
                                                ObtenerDeVirtuosoRangoValores(faceta.ClaveFaceta, pListaFiltros, faceta, omitirPalabrasNoRelevantesSearch, faceta.ElementosVisibles, pPermitirRecursosPrivados, faceta.Inmutable, pEsMovil);
                                                listaTablas.Add(faceta.ClaveFaceta);
                                            }
                                        }
                                        else if (faceta.TipoPropiedad.Equals(TipoPropiedadFaceta.Calendario) || faceta.TipoPropiedad.Equals(TipoPropiedadFaceta.CalendarioConRangos))
                                        {
                                            if (!listaTablas.Contains(faceta.ClaveFaceta))
                                            {
                                                List<int> rangos = new List<int>();
                                                rangos.Add((DateTime.Now.Year * 100 + DateTime.Now.Month - 1) * 100);
                                                rangos.Add((DateTime.Now.Year * 100 + DateTime.Now.Month + 1) * 100);

                                                FacetadoDS facetadoCarga = new FacetadoDS();

                                                Dictionary<string, List<string>> listaFiltrosAux = new Dictionary<string, List<string>>(listaFiltros);
                                                if (listaFiltrosAux.ContainsKey(faceta.ClaveFaceta))
                                                {
                                                    listaFiltrosAux.Remove(faceta.ClaveFaceta);
                                                }

                                                mFacetadoCL.ObtenerFaceta(mGrafoID, mFacetadoDS, faceta.ClaveFaceta, listaFiltrosAux, listaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), faceta.TipoDisenio, 0, 0, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, faceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, omitirPalabrasNoRelevantesSearch, faceta.Reciproca, faceta.TipoPropiedad, FiltrosSearchPersonalizados, faceta.Inmutable, pEsMovil);
                                                listaTablas.Add(faceta.ClaveFaceta);
                                            }
                                        }
                                        else if (faceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Multiple))
                                        {
                                            if (!listaTablas.Contains(faceta.ClaveFaceta))
                                            {
                                                ObtenerDeVirtuosoFacetaMultiple(faceta.ClaveFaceta, pListaFiltros, faceta, omitirPalabrasNoRelevantesSearch, faceta.ElementosVisibles, pPermitirRecursosPrivados, faceta.Inmutable, pEsMovil);
                                                listaTablas.Add(faceta.ClaveFaceta);
                                            }
                                        }
                                        else
                                        {
                                            if (!listaTablas.Contains(faceta.ClaveFaceta) || mFacetadoDSAuxPorFaceta.ContainsKey(faceta.ClaveFaceta))
                                            {
                                                #region FiltroProyectoID para facetas no categorias

                                                int limite = faceta.ElementosVisibles * 2;
                                                FacetadoDS facetadoCarga = null;

                                                if ((faceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemantico || faceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado) && (faceta.FilaElementoEntity is FacetaFiltroProyecto || faceta.FilaElementoEntity is FacetaFiltroHome) && !string.IsNullOrEmpty(faceta.FiltroProyectoID) && faceta.FiltroProyectoID.Split(';')[0].Contains("-"))
                                                {
                                                    limite = 0;

                                                    if (!mFacetadoDSAuxPorFaceta.ContainsKey(faceta.ClaveFaceta))
                                                    {
                                                        mFacetadoDSAuxPorFaceta.Add(faceta.ClaveFaceta, new List<KeyValuePair<string, FacetadoDS>>());
                                                    }

                                                    facetadoCarga = new FacetadoDS();
                                                    mFacetadoDSAuxPorFaceta[faceta.ClaveFaceta].Add(new KeyValuePair<string, FacetadoDS>(faceta.FiltroProyectoID, facetadoCarga));
                                                }
                                                else
                                                {
                                                    facetadoCarga = mFacetadoDS;
                                                }

                                                #endregion

                                                mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoCarga, faceta.ClaveFaceta, listaFiltros, listaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), faceta.TipoDisenio, 0, limite, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, false, null, faceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, omitirPalabrasNoRelevantesSearch, faceta.Reciproca, faceta.TipoPropiedad, FiltrosSearchPersonalizados, faceta.Inmutable, pEsMovil);
                                                string consultaReciproca, claveFaceta = string.Empty;
                                                mFacetadoCL.FacetadoCN.FacetadoAD.ObtenerDatosFiltroReciproco(out consultaReciproca, faceta.ClaveFaceta, out claveFaceta);
                                                listaTablas.Add(claveFaceta);
                                            }
                                        }

                                        #region SubType

                                        if (listaItemsBusquedaExtraBK != null)
                                        {
                                            mFacetadoCL.ListaItemsBusquedaExtra = listaItemsBusquedaExtraBK;
                                        }

                                        #endregion
                                    }
                                }
                                else
                                {
                                    if (!listaTablas.Contains(faceta.ClaveFaceta))
                                    {
                                        DataTable tabla = new DataTable(faceta.ClaveFaceta);
                                        tabla.Columns.Add(faceta.ClaveFaceta.Replace("_", "").Replace(":", "") + "_2", typeof(string));
                                        tabla.Columns.Add("a", typeof(string));
                                        tabla.Rows.Add("null", "0");
                                        mFacetadoDS.Tables.Add(tabla);
                                        listaTablas.Add(faceta.ClaveFaceta);
                                    }
                                }

                                #region Bandeja de mensajes

                                if (listaFiltrosSalvados != null)
                                {
                                    foreach (string facetaSalvada in listaFiltrosSalvados.Keys)
                                    {
                                        pListaFiltros.Add(facetaSalvada, listaFiltrosSalvados[facetaSalvada]);
                                    }
                                }

                                #endregion
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Ha fallado al cargar una faceta, pero sigo con el resto
                        mUtilServicios.EnviarErrorYGuardarLog($"Error: {ex.Message}\r\nPila: {ex.StackTrace}", "errorFaceta", mEsBot);
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(extraContexto))
                        {
                            mFiltroContextoWhere = mFiltroContextoWhere.Replace(extraContexto, "");

                            if (mFiltroContextoWhere == "")
                            {
                                mFiltroContextoWhere = null;
                            }
                        }
                    }
                }
            }

            if (plegadas && listaFacetasPlegadas.Count > 0)
            {
                ObtenerFacetasPlegadas(listaFacetasPlegadas, listaFacetasPlegadasExtraContexto, excluirPersonas, pPermitirRecursosPrivados, omitirPalabrasNoRelevantesSearch);
            }

            if (usarHilos && listaTareasFacetas.Count > 0)
            {
                Task.WaitAll(listaTareasFacetas.ToArray());
            }

            for (int i = inicio; i < fin; i++)
            {
                try
                {
                    if (listaFacetasRecorrer.Count > i)
                    {
                        Faceta faceta = listaFacetasRecorrer[i];

                        bool cargarFaceta = !faceta.OcultaEnFacetas;

                        if (mTipoBusqueda == TipoBusqueda.VerRecursosPerfil)
                        {
                            string claveFaceta2 = faceta.ClaveFaceta;
                            if (!claveFaceta2.Equals("skos:ConceptID") && !claveFaceta2.Equals("sioc_t:Tag") && !claveFaceta2.Equals("gnoss:hastipodoc") && !claveFaceta2.Equals("gnoss:hasextension"))
                            {
                                cargarFaceta = false;
                            }
                        }

                        if (cargarFaceta)
                        {
                            string claveFaceta = faceta.ClaveFaceta;
                            string consultaReciproca = string.Empty;
                            mFacetadoCL.FacetadoCN.FacetadoAD.ObtenerDatosFiltroReciproco(out consultaReciproca, faceta.ClaveFaceta, out claveFaceta);

                            bool pintar = (string.IsNullOrEmpty(this.mFaceta) || this.mFaceta.Equals(claveFaceta)) && (mFacetadoDSAuxPorFaceta.ContainsKey(claveFaceta) || (mFacetadoDS.Tables.Contains(claveFaceta) && (mFacetadoDS.Tables[claveFaceta].Rows.Count > 0 || (plegadas) || (mTipoBusqueda == TipoBusqueda.Mensajes && claveFaceta == "dce:type") || (faceta != null && faceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemantico || faceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado && mListaFiltros.ContainsKey(faceta.ClaveFaceta)))));

                            //Las facetas de tipo calendario deben pintarse aunque no haya resultados en el DS.
                            pintar = pintar || faceta.TipoDisenio == TipoDisenio.Calendario;
                            pintar = pintar || faceta.TipoDisenio == TipoDisenio.CalendarioConRangos;

                            if (pintar)
                            {
                                string nombreFaceta = faceta.Nombre;
                                int limite = -1;

                                if (faceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Categoria) && mCargarArbolCategorias)
                                {
                                    faceta.AlgoritmoTransformacion = TiposAlgoritmoTransformacion.CategoriaArbol;
                                }
                                else
                                {
                                    limite = faceta.ElementosVisibles; //limite
                                }

                                if (claveFaceta != "gnoss:haspublicador" || mTipoBusqueda != TipoBusqueda.Contribuciones || mGrafoID == null || (new OrganizacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication).ExisteOrganizacionPorOrganizacionID(mGrafoID.Substring(mGrafoID.LastIndexOf("/") + 1))))
                                {
                                    CargarFacetaDinamica(limite, faceta.ElementosVisibles, faceta, listaFacetasDevolver, plegadas, !string.IsNullOrEmpty(mFaceta));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Ha fallado al cargar una faceta, pero sigo con el resto
                    mUtilServicios.EnviarErrorYGuardarLog($"Error: {ex.Message}\r\nPila: {ex.StackTrace}", "errorFaceta", mEsBot);
                }
            }

            //Se agregan los filtros en el panel de filtros, para que el usuario pueda quitarlos. 
            if (((mListaFiltrosFacetasNombreReal.Count > 0) || (!string.IsNullOrEmpty(mFiltroContextoNombre) && !string.IsNullOrEmpty(mFiltroContextoWhere))) /*&& (!mEsBot)*/)
            {
                //Se agrega el filtro de la búsqueda de contexto en caso de que sea necesaria
                if (!string.IsNullOrEmpty(mFiltroContextoNombre) && !string.IsNullOrEmpty(mFiltroContextoWhere))
                {
                    if (mFiltroContextoNombre.Length > 50)
                    {
                        listaFiltrosDevolver.Add(AgregarElementoAFaceta("contexto", $"<span title=\"{mFiltroContextoNombre}\">{mFiltroContextoNombre.Substring(0, 50)}...</span>", mFiltroContextoNombre, -1, true, true, TiposAlgoritmoTransformacion.Ninguno));
                    }
                    else
                    {
                        listaFiltrosDevolver.Add(AgregarElementoAFaceta("contexto", $"<span title='prueba'>{mFiltroContextoNombre}</span>", mFiltroContextoNombre, -1, true, true, TiposAlgoritmoTransformacion.Ninguno));
                    }
                }

                foreach (string clave in mListaFiltrosFacetasNombreReal.Keys)
                {
                    bool pintarFiltroQueNoSeaTesauroSemantico = !GestorFacetasOriginal.ListaFacetasPorClave.ContainsKey(clave);
                    pintarFiltroQueNoSeaTesauroSemantico = pintarFiltroQueNoSeaTesauroSemantico || (GestorFacetasOriginal.ListaFacetasPorClave[clave].AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemantico && GestorFacetasOriginal.ListaFacetasPorClave[clave].AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado);

                    //Problema nombre filtro Tesauro Semántico
                    pintarFiltroQueNoSeaTesauroSemantico = pintarFiltroQueNoSeaTesauroSemantico || (!(GestorFacetasOriginal.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroProyecto) && !(GestorFacetasOriginal.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroHome) && GestorFacetasOriginal.ListaFacetasPorClave[clave].AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemantico && GestorFacetasOriginal.ListaFacetasPorClave[clave].AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado);

                    pintarFiltroQueNoSeaTesauroSemantico = pintarFiltroQueNoSeaTesauroSemantico || ((GestorFacetasOriginal.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroProyecto || GestorFacetasOriginal.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroHome) && GestorFacetasOriginal.ListaFacetasPorClave[clave].FiltroProyectoID.Split(';')[0].Trim() != "" && !GestorFacetasOriginal.ListaFacetasPorClave[clave].FiltroProyectoID.Split(';')[0].Contains("-"));

                    if (pintarFiltroQueNoSeaTesauroSemantico)
                    {
                        if (GestorFacetas.ListaFacetasPorClave.ContainsKey(clave) && GestorFacetas.ListaFacetasPorClave[clave].OcultaEnFiltros)
                        {
                            continue;
                        }
                        int numElem = 0;
                        foreach (string valor in mListaFiltrosFacetasNombreReal[clave].ToArray())
                        {
                            if (pListaFiltros.ContainsKey(clave) && pListaFiltros[clave].Count > numElem)
                            {
                                //Si el filtro no contiene 2 puntos ni empieza por http, ni es search ni search personalizado no se tiene en cuenta
                                if (clave.Contains(":") || clave.Contains(";") || clave.StartsWith("http") || clave.ToLower() == "search" || FiltrosSearchPersonalizados.ContainsKey(clave))
                                {
                                    string parametro = pListaFiltros[clave][numElem++];

                                    TipoPropiedadFaceta tipoPropiedad = TipoPropiedadFaceta.NULL;
                                    if (GestorFacetasOriginal.ListaFacetasPorClave.ContainsKey(clave))
                                    {
                                        tipoPropiedad = GestorFacetasOriginal.ListaFacetasPorClave[clave].TipoPropiedad;
                                    }

                                    string valorReal = ObtenerNombreRealFiltro(mListaFiltrosFacetasNombreReal[clave], clave, parametro, tipoPropiedad);

                                    TiposAlgoritmoTransformacion algoritmoTransformacion = TiposAlgoritmoTransformacion.Ninguno;

                                    if (GestorFacetasOriginal.ListaFacetasPorClave.ContainsKey(clave))
                                    {
                                        algoritmoTransformacion = GestorFacetasOriginal.ListaFacetasPorClave[clave].AlgoritmoTransformacion;
                                    }

                                    if (mListaFiltrosFacetasUsuario.ContainsKey(clave) && mListaFiltrosFacetasUsuario[clave].Contains(parametro) && (mTipoBusqueda != TipoBusqueda.Mensajes || clave != "dce:type"))
                                    {
                                        //En el espacio personal hay que pasarle los parametros búsqueda.
                                        if (mTipoBusqueda == TipoBusqueda.EditarRecursosPerfil)
                                        {
                                            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();
                                            if (parametro.Contains(":"))
                                            {
                                                parametrosElementos.Add(parametro, valorReal);
                                            }
                                            else
                                            {
                                                parametrosElementos.Add(parametro.Substring(parametro.IndexOf(":") + 1), valorReal);
                                            }
                                            listaFiltrosDevolver.Add(AgregarElementoAFaceta(clave, valorReal, parametro, -1, true, 0, false, 0, null, false, parametrosElementos, algoritmoTransformacion, tipoPropiedad));
                                        }
                                        else
                                        {
                                            listaFiltrosDevolver.Add(AgregarElementoAFaceta(clave, valorReal, parametro, -1, true, 0, false, 0, null, false, null, algoritmoTransformacion, tipoPropiedad));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        #region Cargo props Tesauro Semántico

                        string[] arrayTesSem = ObtenerDatosFacetaTesSem(clave);

                        List<string> listaPropsTesSem = new List<string>();
                        listaPropsTesSem.Add(arrayTesSem[2]);
                        listaPropsTesSem.Add(arrayTesSem[3]);

                        List<string> listaEntidadesBusqueda = new List<string>();
                        listaEntidadesBusqueda.AddRange(mListaFiltrosFacetasNombreReal[clave]);

                        if (!TesauroSemDSFaceta.ContainsKey(clave) || GestorFacetas.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroProyecto || GestorFacetas.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroHome)
                        {
                            FacetadoCN facCN = new FacetadoCN(mUtilServicios.UrlIntragnoss, mEntityContext, mLoggingService, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                            FacetadoDS facetadoTesSemDS = facCN.ObtenerValoresPropiedadesEntidades(mGrafoID, listaEntidadesBusqueda, listaPropsTesSem, true);
                            facCN.Dispose();

                            if (TesauroSemDSFaceta.ContainsKey(clave))
                            {
                                TesauroSemDSFaceta[clave].Dispose();
                                TesauroSemDSFaceta.Remove(clave);
                            }

                            TesauroSemDSFaceta.Add(clave, facetadoTesSemDS);
                        }

                        #endregion

                        if (GestorFacetasOriginal.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroProyecto)
                        {
                            int numElem = 0;
                            string parametro = null;
                            foreach (string valor in mListaFiltrosFacetasNombreReal[clave].ToArray())
                            {
                                if (pListaFiltros[clave].Count > numElem)
                                {
                                    parametro = pListaFiltros[clave][numElem++];

                                    string valorReal = ObtenerPropTesSem(TesauroSemDSFaceta[clave], arrayTesSem[3], valor);
                                    if (GestorFacetasOriginal.ListaFacetasPorClave.ContainsKey(clave))
                                    {
                                        listaFiltrosDevolver.Add(AgregarElementoAFaceta(clave, valorReal, parametro, -1, true, false, GestorFacetasOriginal.ListaFacetasPorClave[clave].AlgoritmoTransformacion));
                                    }
                                    else
                                    {
                                        listaFiltrosDevolver.Add(AgregarElementoAFaceta(clave, valorReal, parametro, -1, true, false, TiposAlgoritmoTransformacion.Ninguno));
                                    }
                                }
                            }
                        }
                        else
                        {
                            int numElem = 0;
                            string nombreFacetaUnida = "";
                            string parametro = null;

                            foreach (string valor in mListaFiltrosFacetasNombreReal[clave].ToArray())
                            {
                                if (pListaFiltros[clave].Count > numElem)
                                {
                                    parametro = pListaFiltros[clave][numElem++];

                                    string valorReal = ObtenerPropTesSem(TesauroSemDSFaceta[clave], arrayTesSem[3], valor);

                                    if (mListaFiltrosFacetasUsuario.ContainsKey(clave) && mListaFiltrosFacetasUsuario[clave].Contains(parametro) && (mTipoBusqueda != TipoBusqueda.Mensajes || clave != "dce:type"))
                                    {
                                        if ((GestorFacetas.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroProyecto || GestorFacetas.ListaFacetasPorClave[clave].FilaElementoEntity is FacetaFiltroHome) && GestorFacetas.ListaFacetasPorClave[clave].FiltroProyectoID.Split(';')[0].Contains("-"))
                                        {
                                            if (GestorFacetas.ListaFacetasPorClave.ContainsKey(clave))
                                            {
                                                listaFiltrosDevolver.Add(AgregarElementoAFaceta(clave, valorReal, parametro, -1, true, false, GestorFacetas.ListaFacetasPorClave[clave].AlgoritmoTransformacion));
                                            }
                                            else
                                            {
                                                listaFiltrosDevolver.Add(AgregarElementoAFaceta(clave, valorReal, parametro, -1, true, false, TiposAlgoritmoTransformacion.Ninguno));
                                            }
                                        }
                                        else
                                        {
                                            nombreFacetaUnida += $"{valorReal} > ";
                                        }
                                    }
                                }
                            }

                            if (nombreFacetaUnida != "")
                            {
                                nombreFacetaUnida = nombreFacetaUnida.Substring(0, nombreFacetaUnida.Length - 3);

                                if (GestorFacetas.ListaFacetasPorClave.ContainsKey(clave))
                                {
                                    listaFiltrosDevolver.Add(AgregarElementoAFaceta(clave, nombreFacetaUnida, parametro, -1, true, false, GestorFacetas.ListaFacetasPorClave[clave].AlgoritmoTransformacion));
                                }
                                else
                                {
                                    listaFiltrosDevolver.Add(AgregarElementoAFaceta(clave, nombreFacetaUnida, parametro, -1, true, false, TiposAlgoritmoTransformacion.Ninguno));
                                }
                            }
                        }
                    }
                }
            }

            return new KeyValuePair<List<FacetModel>, List<FacetItemModel>>(listaFacetasDevolver, listaFiltrosDevolver);
        }

        [NonAction]
        private Dictionary<string, List<string>> EliminarFiltrosDeListaFiltros(Dictionary<string, List<string>> pListaFiltros, Dictionary<string, List<string>> pListaFiltrosEliminar)
        {
            Dictionary<string, List<string>> listaFiltrosNuevos = new Dictionary<string, List<string>>(pListaFiltros);
            foreach (string key in pListaFiltrosEliminar.Keys)
            {
                if (listaFiltrosNuevos.ContainsKey(key))
                {
                    // Hago un new de la lista porque si no modifico la lista original
                    listaFiltrosNuevos[key] = new List<string>(listaFiltrosNuevos[key]);
                    foreach (string valor in pListaFiltrosEliminar[key])
                    {
                        if (listaFiltrosNuevos[key].Contains(valor))
                        {
                            listaFiltrosNuevos[key].Remove(valor);
                        }
                    }

                    if (listaFiltrosNuevos[key].Count == 0)
                    {
                        listaFiltrosNuevos.Remove(key);
                    }
                }
            }

            return listaFiltrosNuevos;
        }

        [NonAction]
        private void ObtenerTituloFacetasHilo(string pProyectoID, FacetadoDS pFacetadoDS, Dictionary<string, List<string>> pListaFiltros, List<string> pListaFiltrosExtra, bool pEstaEnMyGnoss, bool pEsMiembroComunidad, bool pEsInvitado, string pIdentidadID, int pInicio, int pLimite, List<string> pSemanticos, string pFiltroContextoWhere, TipoProyecto pTipoProyecto, List<int> pListaRangos, bool pUsarHilos, bool pExcluirPersonas, bool pPermitirRecursosPrivados, bool pOmitirPalabrasNoRelevantesSearch, Dictionary<string, Tuple<string, string, string, bool>> pFiltrosSearchPersonalizados, Dictionary<string, int> pListaFacetas, Dictionary<string, string> pListaFacetasExtraContexto)
        {
            mFacetadoCL.ObtenerTituloFacetas(mGrafoID, pFacetadoDS, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), 0, 100, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, null, false, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, FiltrosSearchPersonalizados, pListaFacetas, pListaFacetasExtraContexto);
        }

        [NonAction]
        private void ObtenerFacetasPlegadas(Dictionary<string, int> pListaFacetasPlegadas, Dictionary<string, string> pListaFacetasPlegadasExtraContexto, bool pExcluirPersonas, bool pPermitirRecursosPrivados, bool pOmitirPalabrasNoRelevantesSearch)
        {
            List<string> listaFacetasOriginal = pListaFacetasPlegadas.Keys.ToList();
            if (mParametroProyecto.ContainsKey(ParametroAD.FacetasCostosasTerceraPeticion))
            {
                List<string> facetasCostosas = new List<string>(mParametroProyecto[ParametroAD.FacetasCostosasTerceraPeticion].Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries));
                List<Task> tareasFacetasCostosas = new List<Task>();
                List<FacetadoDS> listaFacetadoDSCostosos = new List<FacetadoDS>();

                if (facetasCostosas.Count == 1 && facetasCostosas[0].ToLower().Equals("todas"))
                {
                    //Queremos que todas las facetas se obtengan simultáneamente
                    facetasCostosas.Clear();
                    facetasCostosas.AddRange(pListaFacetasPlegadas.Keys);
                }

                foreach (string facetaCostosa in facetasCostosas)
                {
                    if (pListaFacetasPlegadas.ContainsKey(facetaCostosa))
                    {
                        Dictionary<string, int> listaAux = new Dictionary<string, int>();
                        listaAux.Add(facetaCostosa, pListaFacetasPlegadas[facetaCostosa]);
                        FacetadoDS facetadoDS = new FacetadoDS();
                        listaFacetadoDSCostosos.Add(facetadoDS);
                        //Creo una nueva lista porque se modifica dentro del FacetadoAD, 
                        Dictionary<string, List<string>> listaFiltrosCopia = new Dictionary<string, List<string>>(mListaFiltros);

                        Task tareaFacetaCostosa = Task.Factory.StartNew(() =>
                        ObtenerTituloFacetasHilo(mGrafoID, facetadoDS, listaFiltrosCopia, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), 0, 100, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, null, false, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, FiltrosSearchPersonalizados, listaAux, pListaFacetasPlegadasExtraContexto));
                        pListaFacetasPlegadas.Remove(facetaCostosa);
                        tareasFacetasCostosas.Add(tareaFacetaCostosa);
                    }
                }

                if (pListaFacetasPlegadas.Count > 0)
                {
                    Task tareaTerceraPeticion = Task.Factory.StartNew(() =>
                        ObtenerTituloFacetasHilo(mGrafoID, mFacetadoDS, mListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), 0, 100, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, null, false, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, FiltrosSearchPersonalizados, pListaFacetasPlegadas, pListaFacetasPlegadasExtraContexto));

                    tareaTerceraPeticion.Wait();
                }
                else
                {
                    //Creo la tabla facetas vacía, para irla llenando con lo que han traído los hilos
                    mFacetadoDS.Tables.Add("Facetas");
                    mFacetadoDS.Tables["Facetas"].Columns.Add("propiedad", typeof(string));
                    mFacetadoDS.Tables["Facetas"].Columns.Add("orden", typeof(int));
                }

                foreach (Task tarea in tareasFacetasCostosas)
                {
                    if (!tarea.IsCompleted && !tarea.IsCanceled && !tarea.IsFaulted)
                    {
                        tarea.Wait();
                    }
                }

                foreach (FacetadoDS facetadoDS in listaFacetadoDSCostosos)
                {
                    if (facetadoDS.Tables.Count > 0 && facetadoDS.Tables[0].Rows.Count > 0)
                    {
                        foreach (DataRow filaFaceta in facetadoDS.Tables[0].Rows)
                        {
                            string faceta = (string)filaFaceta[0];
                            //Búsco su posicion original
                            filaFaceta[1] = listaFacetasOriginal.IndexOf(faceta) + 1;
                            mFacetadoDS.Tables["Facetas"].ImportRow(filaFaceta);
                        }
                    }
                    facetadoDS.Dispose();
                }
            }
            else
            {
                mFacetadoCL.ObtenerTituloFacetas(mGrafoID, mFacetadoDS, mListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), 0, 100, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, null, false, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, FiltrosSearchPersonalizados, pListaFacetasPlegadas, pListaFacetasPlegadasExtraContexto);
            }

            foreach (DataRow faceta in mFacetadoDS.Tables["Facetas"].Select("", "orden"))
            {
                string nombreFaceta = (string)faceta[0];
                if (!mFacetadoDS.Tables.Contains(nombreFaceta))
                {
                    mFacetadoDS.Tables.Add(nombreFaceta);
                }
            }
        }

        [NonAction]
        private string ObtenerExtraContextoTesauroSemantico(Faceta pFaceta)
        {
            string extraContexto = "";
            if (pFaceta != null && (pFaceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemantico || pFaceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado))
            {
                string[] arrayTesSem = ObtenerDatosFacetaTesSem(pFaceta.ClaveFaceta);

                extraContexto = $"AgreAFiltro={pFaceta.ClaveFaceta},";

                string nivelSemantico = null;
                string source = null;
                string idioma = null;
                if ((pFaceta.FilaElementoEntity is FacetaFiltroProyecto || pFaceta.FilaElementoEntity is FacetaFiltroHome) && !string.IsNullOrEmpty(pFaceta.FiltroProyectoID) && pFaceta.FiltroProyectoID.Contains(";"))
                {
                    nivelSemantico = pFaceta.FiltroProyectoID.Split(';')[0];
                    source = pFaceta.FiltroProyectoID.Split(';')[1];

                    if (nivelSemantico.Contains("[MultiIdioma]"))
                    {
                        idioma = UtilIdiomas.LanguageCode;
                        nivelSemantico = nivelSemantico.Replace("[MultiIdioma]", "");
                    }
                }

                if (!string.IsNullOrEmpty(nivelSemantico))
                {
                    if (!nivelSemantico.Contains("-"))
                    {
                        string filtroIdioma = "";

                        if (idioma != null)
                        {
                            filtroIdioma = $" AND lang(@o@)='{idioma}'";
                        }

                        if (!string.IsNullOrEmpty(source))
                        {
                            extraContexto += $" @s@ <{arrayTesSem[5]}> \"{source}\".";
                        }

                        extraContexto += $" @s@ <{arrayTesSem[6]}> ?nivelTesSem. FILTER(?nivelTesSem={nivelSemantico}{filtroIdioma}) ";
                    }
                    else
                    {
                        int inicioRangoNivel = int.Parse(nivelSemantico.Split('-')[0]);
                        int finRangoNivel = int.Parse(nivelSemantico.Split('-')[1]);

                        if (!string.IsNullOrEmpty(source))
                        {
                            extraContexto += $"@o@ <{arrayTesSem[5]}> \"{source}\". ";
                        }

                        extraContexto += "{";

                        if (inicioRangoNivel == finRangoNivel)
                        {
                            extraContexto += $"@o@ <{arrayTesSem[6]}> ?nivelTesSem. FILTER(?nivelTesSem = {inicioRangoNivel})}}";
                        }
                        else
                        {
                            extraContexto += $"@o@ <{arrayTesSem[6]}> ?nivelTesSem. FILTER(?nivelTesSem >= {inicioRangoNivel} AND ?nivelTesSem <= {finRangoNivel})}}";
                        }
                    }
                }
                else if (!mListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                {
                    if (string.IsNullOrEmpty(source))
                    {
                        extraContexto += $"{{?sujCollTaxo <{arrayTesSem[1]}> ";
                    }
                    else
                    {
                        extraContexto += $"?sujCollTaxo <{arrayTesSem[1]}> @o@. @o@ <{arrayTesSem[5]}> \"{source}\".";
                    }
                }
                else
                {
                    string catPadre = mListaFiltros[pFaceta.ClaveFaceta][mListaFiltros[pFaceta.ClaveFaceta].Count - 1];
                    if (string.IsNullOrEmpty(source))
                    {
                        extraContexto += $"{{<{catPadre}> <{arrayTesSem[4]}> ";
                    }
                    else
                    {
                        extraContexto += $"<{catPadre}> <{arrayTesSem[4]}> @o@. @o@ <{arrayTesSem[5]}> \"{source}\".";
                    }
                }

                if (!extraContexto.EndsWith("|"))
                {
                    extraContexto += " |";
                }

                mFiltroContextoWhere = extraContexto + mFiltroContextoWhere;
            }
            else if (pFaceta != null && pFaceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.MultiIdioma)
            {
                extraContexto = $"idioma={UtilIdiomas.LanguageCode}|";
                mFiltroContextoWhere = extraContexto + mFiltroContextoWhere;
            }

            return extraContexto;
        }

        [NonAction]
        private void AjusteInicioFinFacetasConfigurado(int pNumeroFacetas, out int inicio, out int fin, List<Faceta> pListaFacetas)
        {
            int numFacetasPrimeraPeticion = 2;
            int numFacetasSegundaPeticion = 2;

            inicio = 0;
            fin = pNumeroFacetas;

            if (mParametroProyecto.ContainsKey(ParametroAD.NumeroFacetasPrimeraPeticion))
            {
                int.TryParse(mParametroProyecto[ParametroAD.NumeroFacetasPrimeraPeticion], out numFacetasPrimeraPeticion);
            }

            if (mParametroProyecto.ContainsKey(ParametroAD.NumeroFacetasSegundaPeticion))
            {
                int.TryParse(mParametroProyecto[ParametroAD.NumeroFacetasSegundaPeticion], out numFacetasSegundaPeticion);
            }

            if (pNumeroFacetas.Equals(1))
            {
                inicio = 0;
                fin = numFacetasPrimeraPeticion;
            }
            else if (pNumeroFacetas.Equals(2))
            {
                inicio = numFacetasPrimeraPeticion;
                fin = numFacetasSegundaPeticion + numFacetasPrimeraPeticion;
            }
            else if (pNumeroFacetas.Equals(3))
            {
                inicio = numFacetasPrimeraPeticion + numFacetasSegundaPeticion;
                fin = pListaFacetas.Count;
            }
        }

        private void AjusteInicioFinFacetas(int pNumeroFacetas, out int inicio, out int fin, ref List<Faceta> pListaFacetas)
        {
            inicio = 0;
            fin = pNumeroFacetas;

            if (!GruposPorTipo)
            {
                //Si no se agrupa por tipos se traen normalmente
                if (pNumeroFacetas < 2 || pNumeroFacetas > pListaFacetas.Count)
                {
                    fin = pListaFacetas.Count;
                }

                if (string.IsNullOrEmpty(mFaceta))
                {
                    //NOTA:
                    //si las dos primeras facetas son etiquetas y
                    //categorías, solo se traerá una de ellas (ya que las dos son muy pesadas y
                    //puede costar mucho la carga de ese primer par)

                    if (pNumeroFacetas == 1)
                    {
                        inicio = 0;
                        fin = 2;

                        if (ContieneFacetasTagsYCatEn(0, 1))
                        {
                            fin = 1;
                        }

                        while (ContieneFacetasCatYCatEn(fin - 1, fin))
                        {
                            fin++;
                        }
                    }
                    else if (pNumeroFacetas == 2)
                    {
                        inicio = 2;
                        fin = 4;

                        if (ContieneFacetasTagsYCatEn(0, 1))
                        {
                            inicio = 1;
                            fin = 3;
                        }

                        while (ContieneFacetasCatYCatEn(inicio - 1, inicio))
                        {
                            inicio++;
                            fin++;
                        }

                        if (ContieneFacetasTagsYCatEn(inicio, inicio + 1))
                        {
                            fin--;
                        }

                        while (ContieneFacetasCatYCatEn(fin - 1, fin))
                        {
                            fin++;
                        }
                    }
                    else if (pNumeroFacetas == 3)
                    {
                        inicio = 4;
                        fin = pListaFacetas.Count;

                        int inicioAnt = 2;
                        if (ContieneFacetasTagsYCatEn(0, 1))
                        {
                            inicioAnt = 1;
                            inicio = 3;
                        }

                        while (ContieneFacetasCatYCatEn(inicioAnt - 1, inicioAnt))
                        {
                            inicioAnt++;
                            inicio++;
                        }

                        if (ContieneFacetasTagsYCatEn(inicioAnt, inicioAnt + 1))
                        {
                            inicio--;
                        }

                        while (ContieneFacetasCatYCatEn(inicio - 1, inicio))
                        {
                            inicio++;
                        }
                        while (ContieneFacetasCatYCatEn(fin - 1, fin))
                        {
                            fin++;
                        }
                    }

                    if (inicio >= pListaFacetas.Count)
                    {
                        inicio = 0;
                        fin = 0;
                    }
                    else if (fin > pListaFacetas.Count)
                    {
                        fin = pListaFacetas.Count;
                    }
                }
            }
            else
            {
                AjustarFacetasAgrupadas(pNumeroFacetas, out inicio, out fin, ref pListaFacetas);
            }
        }

        [NonAction]
        private void EliminarFacetasNoAplicables(int pNumeroFacetas, string pTipoBusqueda, FacetadoDS pFacetadoComprobacionRdfTypeDS)
        {//Eliminar solo de memoria
            if (string.IsNullOrEmpty(mFaceta))
            {
                List<FacetaObjetoConocimiento> tablaConfi = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento;

                if (mTipoBusqueda.Equals(TipoBusqueda.EditarRecursosPerfil))
                {
                    List<FacetaObjetoConocimiento> filas = tablaConfi.Where(item => item.Faceta.Equals("rdf:type") || item.Faceta.Equals("gnoss:hasEstado") || item.Faceta.Equals("gnoss:hasEstadoPP")).ToList();
                    foreach (FacetaObjetoConocimiento fila in filas)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(fila);
                    }
                }

                if ((mTipoBusqueda.Equals(TipoBusqueda.PersonasYOrganizaciones) || pTipoBusqueda.Equals("PersonaInicial") || mTipoBusqueda.Equals(TipoBusqueda.BusquedaAvanzada)) && (!mAdministradorQuiereVerTodasLasPersonas || !mProyectoID.Equals(ProyectoAD.MetaProyecto)))
                {
                    // las facetas estado corrección y fecha de alta solo se muestra en personas y organizaciones al administrador en mygnoss o a cualquier usuario fuera de la página de personas y organizaciones (por ahora solo sale en comunidades).
                    List<FacetaObjetoConocimiento> filas = tablaConfi.Where(item => item.Faceta.Equals("gnoss:hasfechaAlta")).ToList();
                    foreach (FacetaObjetoConocimiento fila in filas)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(fila);
                    }

                    filas = tablaConfi.Where(item => item.Faceta.Equals("gnoss:hasEstadoCorreccion")).ToList();
                    foreach (FacetaObjetoConocimiento fila in filas)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(fila);
                    }
                }

                if (!mAdministradorQuiereVerTodasLasPersonas)
                {
                    List<FacetaObjetoConocimiento> filas = tablaConfi.Where(item => item.Faceta.Equals("gnoss:userstatus") || item.Faceta.Equals("gnoss:rol")).ToList();
                    foreach (FacetaObjetoConocimiento fila in filas)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(fila);
                    }
                    List<FacetaObjetoConocimientoProyecto> filas2 = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(item => item.Faceta.Equals("gnoss:userstatus") || item.Faceta.Equals("gnoss:rol")).ToList();
                    foreach (FacetaObjetoConocimientoProyecto fila in filas2)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Remove(fila);
                    }
                }

                if (!mEstaEnProyecto)
                {
                    //Las facetas editores y publicadores solo le sale a los usuarios que son miembros de la comunidad
                    List<FacetaObjetoConocimiento> filas = tablaConfi.Where(item => item.Faceta.Equals("gnoss:haseditor")).ToList();
                    foreach (FacetaObjetoConocimiento fila in filas)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(fila);
                    }

                    filas = tablaConfi.Where(item => item.Faceta.Equals("gnoss:haspublicador")).ToList();
                    foreach (FacetaObjetoConocimiento fila in filas)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(fila);
                    }
                }

                if (!mMostrarFacetaEstado)
                {
                    //Sólo se muestra la faceta estado de las contribuciones de un usuario si es él mismo el que las está viendo
                    List<FacetaObjetoConocimiento> filas = tablaConfi.Where(item => item.Faceta.Equals("gnoss:hasEstado")).ToList();
                    foreach (FacetaObjetoConocimiento fila in filas)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(fila);
                    }
                }

                if (mTipoBusqueda.Equals(TipoBusqueda.Contribuciones))
                {
                    // La faceta categorías no deben salir cuando son contribuciones 
                    List<FacetaObjetoConocimiento> filas = tablaConfi.Where(item => item.Faceta.Equals("skos:ConceptID")).ToList();
                    foreach (FacetaObjetoConocimiento fila in filas)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(fila);
                    }
                }

                if (mFacetasHomeCatalogo)
                {
                    if (GestorFacetas.FacetasDW.ListaFacetaHome.Count == 0)
                    {
                        List<FacetaObjetoConocimiento> listaFacetaObjetoConocimiento = new List<FacetaObjetoConocimiento>();
                        List<FacetaObjetoConocimientoProyecto> listaFacetaObjetoConocimientoProyectoBorrar = new List<FacetaObjetoConocimientoProyecto>();
                        List<FacetaFiltroProyecto> listaFacetaFiltroProyectoBorrar = new List<FacetaFiltroProyecto>();

                        listaFacetaObjetoConocimiento = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Where(item => !item.ObjetoConocimiento.ToLower().Equals("recurso")).ToList();
                        listaFacetaObjetoConocimientoProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(item => !item.ObjetoConocimiento.ToLower().Equals("recurso")).ToList();
                        listaFacetaFiltroProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(item => !item.ObjetoConocimiento.ToLower().Equals("recurso")).ToList();

                        foreach (FacetaObjetoConocimiento filaObjConProy in listaFacetaObjetoConocimiento)
                        {
                            GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(filaObjConProy);
                        }

                        foreach (FacetaObjetoConocimientoProyecto filaObjConProy in listaFacetaObjetoConocimientoProyectoBorrar)
                        {
                            if (filaObjConProy.Reciproca == 0)
                            {
                                GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Remove(filaObjConProy);
                            }
                        }

                        foreach (FacetaFiltroProyecto filaFiltroProy in listaFacetaFiltroProyectoBorrar)
                        {
                            GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Remove(filaFiltroProy);
                        }

                        List<FacetaFiltroHome> listaFacetaFiltroHome = GestorFacetas.FacetasDW.ListaFacetaFiltroHome.Where(item => !item.ObjetoConocimiento.Equals("Recurso")).ToList();
                        List<FacetaHome> listaFacetaHome = GestorFacetas.FacetasDW.ListaFacetaHome.Where(item => !item.ObjetoConocimiento.Equals("Recurso")).ToList();
                        MarcarFilasBorradasDataSet(listaFacetaFiltroHome, listaFacetaHome);
                    }
                }


                if ((FilaPestanyaBusquedaActual != null && GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyectoPenstanya.Any(item => item.PestanyaID.Equals(FilaPestanyaBusquedaActual.PestanyaID))) || (mPestanyaActualID != Guid.Empty && GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyectoPenstanya.Any(item => item.PestanyaID.Equals(mPestanyaActualID))))
                {
                    Guid pestanyaIDaBuscar = FilaPestanyaBusquedaActual != null ? FilaPestanyaBusquedaActual.PestanyaID : mPestanyaActualID;

                    try
                    {
                        Dictionary<string, List<string>> listObjetosConocimiento = new Dictionary<string, List<string>>();
                        foreach (FacetaObjetoConocimientoProyectoPestanya facetaPestanya in GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyectoPenstanya.Where(item => item.PestanyaID.Equals(pestanyaIDaBuscar)))
                        {
                            if (!listObjetosConocimiento.ContainsKey(facetaPestanya.ObjetoConocimiento))
                            {
                                listObjetosConocimiento.Add(facetaPestanya.ObjetoConocimiento, new List<string>());
                            }
                            listObjetosConocimiento[facetaPestanya.ObjetoConocimiento].Add(facetaPestanya.Faceta);
                        }

                        List<FacetaObjetoConocimiento> listaFacetaObjetoConocimiento = new List<FacetaObjetoConocimiento>();
                        List<FacetaObjetoConocimientoProyecto> listaFacetaObjetoConocimientoProyectoBorrar = new List<FacetaObjetoConocimientoProyecto>();
                        List<FacetaFiltroProyecto> listaFacetaFiltroProyectoBorrar = new List<FacetaFiltroProyecto>();

                        listaFacetaObjetoConocimiento = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Where(item => !listObjetosConocimiento.Keys.Contains(item.ObjetoConocimiento) || !listObjetosConocimiento[item.ObjetoConocimiento].Contains(item.Faceta)).ToList();
                        listaFacetaObjetoConocimientoProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(item => !listObjetosConocimiento.Keys.Contains(item.ObjetoConocimiento) || !listObjetosConocimiento[item.ObjetoConocimiento].Contains(item.Faceta)).ToList();
                        listaFacetaFiltroProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(item => !listObjetosConocimiento.Keys.Contains(item.ObjetoConocimiento) || !listObjetosConocimiento[item.ObjetoConocimiento].Contains(item.Faceta)).ToList();

                        foreach (FacetaObjetoConocimiento filaObjConProy in listaFacetaObjetoConocimiento)
                        {
                            GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(filaObjConProy);
                        }

                        foreach (FacetaObjetoConocimientoProyecto filaObjConProy in listaFacetaObjetoConocimientoProyectoBorrar)
                        {
                            if (filaObjConProy.Reciproca == 0)
                            {
                                GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Remove(filaObjConProy);
                            }
                        }

                        foreach (FacetaFiltroProyecto filaFiltroProy in listaFacetaFiltroProyectoBorrar)
                        {
                            GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Remove(filaFiltroProy);
                        }
                    }
                    catch (Exception ex)
                    {
                        mLoggingService.GuardarLogError(ex);
                        throw;
                    }
                }

                var listaFiltrosComprobar = mListaFiltros;
                List<FacetaObjetoConocimientoProyecto> listaFacetaObjetoConocimientoProyecto = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(item => !string.IsNullOrEmpty(item.Condicion)).ToList();
                foreach (FacetaObjetoConocimientoProyecto filaObjCon in listaFacetaObjetoConocimientoProyecto)
                {
                    bool factCorrecta = ComprobarCondicionEnFiltro(filaObjCon.Condicion, pFacetadoComprobacionRdfTypeDS);

                    if (!factCorrecta)
                    {
                        //MIGRAR EF
                        //var filasFacetaHome = filaObjCon.Fa.GetFacetaHomeRows();
                        //if (filasFacetaHome != null && filasFacetaHome.Length > 0)
                        //{
                        //    foreach (var fila in filasFacetaHome)
                        //    {
                        //        fila.Delete();
                        //    }
                        //}

                        List<FacetaFiltroProyecto> filasFacetas = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(item => item.Faceta.Equals(filaObjCon.Faceta) && item.ObjetoConocimiento.Equals(filaObjCon.ObjetoConocimiento)).ToList();
                        if (filasFacetas != null && filasFacetas.Count > 0)
                        {
                            foreach (FacetaFiltroProyecto filaFacetas in filasFacetas)
                            {
                                filaObjCon.FacetaFiltroProyecto.Remove(filaFacetas);
                            }
                        }

                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Remove(filaObjCon);
                    }
                }
                List<FacetaFiltroProyecto> listaFacetaFiltroProyecto = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(item => !string.IsNullOrEmpty(item.Condicion)).ToList();
                foreach (FacetaFiltroProyecto filaFiltro in listaFacetaFiltroProyecto)
                {
                    if (!ComprobarCondicionEnFiltro(filaFiltro.Condicion, pFacetadoComprobacionRdfTypeDS))
                    {
                        var filaSuperior = filaFiltro.FacetaObjetoConocimientoProyecto;
                        GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Remove(filaFiltro);

                        if (filaSuperior.FacetaFiltroProyecto.Count == 0)
                        {
                            GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Remove(filaSuperior);
                        }
                    }
                }

                if (ParametroProyecto.ContainsKey(ParametroAD.OcultarFacetatasDeOntologiasEnRecursosCuandoEsMultiple) && ParametroProyecto[ParametroAD.OcultarFacetatasDeOntologiasEnRecursosCuandoEsMultiple] == "1" && !mFacetasHomeCatalogo)
                {
                    List<FacetaObjetoConocimiento> listaFacetaObjetoConocimiento = new List<FacetaObjetoConocimiento>();
                    List<FacetaObjetoConocimientoProyecto> listaFacetaObjetoConocimientoProyectoBorrar = new List<FacetaObjetoConocimientoProyecto>();
                    List<FacetaFiltroProyecto> listaFacetaFiltroProyectoBorrar = new List<FacetaFiltroProyecto>();

                    listaFacetaObjetoConocimiento = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Where(item => !item.ObjetoConocimiento.ToLower().Equals("recurso")).ToList();
                    listaFacetaObjetoConocimientoProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(item => !item.ObjetoConocimiento.ToLower().Equals("recurso")).ToList();
                    listaFacetaFiltroProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(item => !item.ObjetoConocimiento.ToLower().Equals("recurso")).ToList();

                    foreach (FacetaObjetoConocimiento filaObjConProy in listaFacetaObjetoConocimiento)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(filaObjConProy);
                    }

                    foreach (FacetaObjetoConocimientoProyecto filaObjConProy in listaFacetaObjetoConocimientoProyectoBorrar)
                    {
                        if (filaObjConProy.Reciproca == 0)
                        {
                            GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Remove(filaObjConProy);
                        }
                    }

                    foreach (FacetaFiltroProyecto filaFiltroProy in listaFacetaFiltroProyectoBorrar)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Remove(filaFiltroProy);
                    }

                    //No eliminar en caso de que sea la home.
                    if (mFacetadoDS.Tables["rdf:type"].Rows.Count > 1)
                    {
                        List<FacetaFiltroHome> listaFacetaFiltroHome = GestorFacetas.FacetasDW.ListaFacetaFiltroHome.Where(item => !item.ObjetoConocimiento.Equals("Recurso")).ToList();
                        List<FacetaHome> listaFacetaHome = GestorFacetas.FacetasDW.ListaFacetaHome.Where(item => !item.ObjetoConocimiento.Equals("Recurso")).ToList();
                        MarcarFilasBorradasDataSet(listaFacetaFiltroHome, listaFacetaHome);
                    }
                }

                if (mFacetadoDS.Tables.Contains("rdf:type") && mFacetadoDS.Tables["rdf:type"].Rows.Count > 0 && !mFacetasHomeCatalogo)
                {
                    //No eliminar en caso de que sea la home.
                    //Eliminamos las facetas que no compartan el objeto de conocimiento con el tipo

                    var listaFiltrada = GestorFacetas.FacetasDW.ListaFacetaFiltroHome;
                    var listaFiltradaFacetaHome = GestorFacetas.FacetasDW.ListaFacetaHome;

                    string select = "";
                    List<string> listaTipos = new List<string>();

                    foreach (DataRow dr in mFacetadoDS.Tables["rdf:type"].Rows)
                    {

                        string tipo = dr[0].ToString();
                        select += $"ObjetoConocimiento <> '{tipo}' AND ";
                        listaTipos.Add(tipo.ToLower());
                    }

                    listaFiltrada = listaFiltrada.Where(item => !listaTipos.Contains(item.ObjetoConocimiento)).ToList();
                    listaFiltradaFacetaHome = listaFiltradaFacetaHome.Where(item => !listaTipos.Contains(item.ObjetoConocimiento)).ToList();

                    List<FacetaObjetoConocimiento> listaFacetaObjetoConocimiento = new List<FacetaObjetoConocimiento>();
                    List<FacetaObjetoConocimientoProyecto> listaFacetaObjetoConocimientoProyectoBorrar = new List<FacetaObjetoConocimientoProyecto>();
                    List<FacetaFiltroProyecto> listaFacetaFiltroProyectoBorrar = new List<FacetaFiltroProyecto>();

                    if (listaTipos.Count > 0)
                    {
                        if (select.Contains("ObjetoConocimiento <> 'comunidad no educativa'") || select.Contains("ObjetoConocimiento <> 'comunidad educativa'"))
                        {
                            select += " ObjetoConocimiento <> 'Comunidad' AND ";
                            listaFiltrada = listaFiltrada.Where(item => !item.ObjetoConocimiento.Equals("Comunidad")).ToList();
                            listaFiltradaFacetaHome = listaFiltradaFacetaHome.Where(item => !item.ObjetoConocimiento.Equals("Comunidad")).ToList();
                            listaFacetaObjetoConocimiento = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Where(facObjCon => !listaTipos.Contains(facObjCon.ObjetoConocimiento.ToLower()) && facObjCon.ObjetoConocimiento.ToLower() != "comunidad" && facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                            listaFacetaObjetoConocimientoProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(facObjCon => !listaTipos.Contains(facObjCon.ObjetoConocimiento.ToLower()) && facObjCon.ObjetoConocimiento.ToLower() != "comunidad" && facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                            listaFacetaFiltroProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(facObjCon => !listaTipos.Contains(facObjCon.ObjetoConocimiento.ToLower()) && facObjCon.ObjetoConocimiento.ToLower() != "comunidad" && facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                        }
                        else
                        {
                            listaFacetaObjetoConocimiento = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Where(facObjCon => !listaTipos.Contains(facObjCon.ObjetoConocimiento.ToLower()) && facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                            listaFacetaObjetoConocimientoProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(facObjCon => !listaTipos.Contains(facObjCon.ObjetoConocimiento.ToLower()) && facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                            listaFacetaFiltroProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(facObjCon => !listaTipos.Contains(facObjCon.ObjetoConocimiento.ToLower()) && facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                        }
                    }
                    else
                    {
                        listaFacetaObjetoConocimiento = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Where(facObjCon => facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                        listaFacetaObjetoConocimientoProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(facObjCon => facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                        listaFacetaFiltroProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(facObjCon => facObjCon.ObjetoConocimiento.ToLower() != "recurso").ToList();
                    }

                    //Los recursos son la clase básica y si se eliminan hay facetas como el rdf:type que no se pintan.
                    select += " ObjetoConocimiento <> 'Recurso'";
                    listaFiltrada = listaFiltrada.Where(item => item.ObjetoConocimiento.Equals("Recurso")).ToList();
                    listaFiltradaFacetaHome = listaFiltradaFacetaHome.Where(item => item.ObjetoConocimiento.Equals("Recurso")).ToList();

                    foreach (FacetaObjetoConocimiento filaObjConProy in listaFacetaObjetoConocimiento)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(filaObjConProy);
                    }

                    foreach (FacetaObjetoConocimientoProyecto filaObjConProy in listaFacetaObjetoConocimientoProyectoBorrar)
                    {
                        if (filaObjConProy.Reciproca == 0)
                        {
                            GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Remove(filaObjConProy);
                        }
                    }

                    List<FacetaFiltroHome> listaAuxiliarFacetaFiltroHome = listaFiltrada;
                    foreach (FacetaFiltroHome filaFiltroHome in listaAuxiliarFacetaFiltroHome)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaFiltroHome.Remove(filaFiltroHome);
                    }

                    foreach (FacetaHome filaFiltroHome in listaFiltradaFacetaHome)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaHome.Remove(filaFiltroHome);
                    }

                    foreach (FacetaFiltroProyecto filaFiltroProy in listaFacetaFiltroProyectoBorrar)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Remove(filaFiltroProy);
                    }
                }

                if (GruposPorTipo && mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1)
                {
                    // Hay que sacar sólo las facetas de un tipo concreto
                    string select = $"ObjetoConocimiento <> '{mListaFiltrosConGrupos["default;rdf:type"][0]}'";
                    List<FacetaFiltroHome> listaFacetaFiltroHome = GestorFacetas.FacetasDW.ListaFacetaFiltroHome.Where(item => !item.ObjetoConocimiento.Equals(mListaFiltrosConGrupos["default;rdf:type"][0])).ToList();
                    List<FacetaHome> listaFacetaHome = GestorFacetas.FacetasDW.ListaFacetaHome.Where(item => !item.ObjetoConocimiento.Equals(mListaFiltrosConGrupos["default;rdf:type"][0])).ToList();
                    MarcarFilasBorradasDataSet(listaFacetaFiltroHome, listaFacetaHome);
                    List<FacetaObjetoConocimiento> listaFacetaObjetoConocimiento = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Where(faceta => faceta.ObjetoConocimiento != mListaFiltrosConGrupos["default;rdf:type"][0]).ToList();
                    List<FacetaObjetoConocimientoProyecto> listaFacetaObjetoConocimientoProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Where(faceta => faceta.ObjetoConocimiento != mListaFiltrosConGrupos["default;rdf:type"][0]).ToList();
                    List<FacetaFiltroProyecto> listaFacetaFiltroProyectoBorrar = GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Where(faceta => faceta.ObjetoConocimiento != mListaFiltrosConGrupos["default;rdf:type"][0]).ToList();

                    foreach (FacetaObjetoConocimiento filaObjConProy in listaFacetaObjetoConocimiento)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaObjetoConocimiento.Remove(filaObjConProy);
                    }

                    foreach (FacetaObjetoConocimientoProyecto filaObjConProy in listaFacetaObjetoConocimientoProyectoBorrar)
                    {
                        if (filaObjConProy.Reciproca == 0)
                        {
                            GestorFacetas.FacetasDW.ListaFacetaObjetoConocimientoProyecto.Remove(filaObjConProy);
                        }
                    }

                    foreach (FacetaFiltroProyecto filaFiltroProy in listaFacetaFiltroProyectoBorrar)
                    {
                        GestorFacetas.FacetasDW.ListaFacetaFiltroProyecto.Remove(filaFiltroProy);
                    }
                }

                //tablaConfi.AcceptChanges();
                GestorFacetas.CargarGestorFacetas();

                GestorFacetas.ReordenarFacetas();

                if (pFacetadoComprobacionRdfTypeDS == null)
                {
                    pFacetadoComprobacionRdfTypeDS = mFacetadoDS;
                }

                if (pFacetadoComprobacionRdfTypeDS.Tables.Contains("rdf:type") && pFacetadoComprobacionRdfTypeDS.Tables["rdf:type"].Rows.Count == 1)
                {
                    //Configuro la búsqueda para que solo tenga un tipo de elemento
                    DataRow myrow = pFacetadoComprobacionRdfTypeDS.Tables["rdf:type"].Rows[0];
                    string tipo = (string)myrow[0];

                    if ((!tipo.Equals(FacetadoAD.BUSQUEDA_CLASE_SECUNDARIA)) && (!tipo.Equals(FacetadoAD.BUSQUEDA_CLASE_UNIVERSIDAD)) && (!tipo.Equals(FacetadoAD.BUSQUEDA_COMUNIDAD_EDUCATIVA)) && (!tipo.Equals(FacetadoAD.BUSQUEDA_COMUNIDAD_NO_EDUCATIVA)))
                    {
                        if ((!mListaFiltrosFacetasUsuario.ContainsKey("rdf:type") || mListaFiltrosFacetasUsuario["rdf:type"].Count == 0) && !GruposPorTipo)
                        {
                            mFacetadoDS.Tables["rdf:type"].Clear();
                        }
                    }
                }
            }
        }

        [NonAction]
        private bool ComprobarCondicionEnFiltro(string pCondicion, FacetadoDS pFacetadoComprobacionRdfTypeDS)
        {
            bool factCorrecta = false;

            string[] condiciones = pCondicion.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var listaFiltrosComprobar = mListaFiltros;

            foreach (string condicion in condiciones)
            {
                if (condicion.ToLower().Equals("user"))
                {
                    listaFiltrosComprobar = mListaFiltrosFacetasUsuario;
                }
                else
                {
                    string[] propiedadValor = condicion.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (propiedadValor.Length > 1)
                    {
                        string propiedad = propiedadValor[0];
                        string valor = propiedadValor[1];

                        if (listaFiltrosComprobar != null && listaFiltrosComprobar.ContainsKey(propiedad))
                        {
                            foreach (string valorFiltro in listaFiltrosComprobar[propiedad])
                            {
                                if (propiedad.EndsWith(FacetaAD.Faceta_Gnoss_SubType))
                                {
                                    string valorComprobar = FacetaAD.ObtenerValorAplicandoNamespaces(valorFiltro, GestorFacetas.FacetasDW.ListaOntologiaProyecto, true);

                                    if (valor.Contains(valorComprobar))
                                    {
                                        factCorrecta = true;
                                        break;
                                    }
                                }
                                else if (valor.Equals(valorFiltro))
                                {
                                    factCorrecta = true;
                                    break;
                                }
                            }
                        }
                        else if (propiedad.Equals("rdf:type"))
                        {
                            if (pFacetadoComprobacionRdfTypeDS.Tables.Contains("rdf:type") && pFacetadoComprobacionRdfTypeDS.Tables["rdf:type"].Rows.Count == 1 && pFacetadoComprobacionRdfTypeDS.Tables["rdf:type"].Rows[0][0].Equals(valor))
                            {
                                factCorrecta = true;
                                break;
                            }
                        }
                    }

                    if (factCorrecta)
                    {
                        break;
                    }
                }
            }

            return factCorrecta;
        }

        /// <summary>
        /// Método para eliminar las filas de todas las tablas del DS, sino fallan algunos filtros.
        /// </summary>
        /// <param name="pSelect">Select desde el que se va a borrar.</param>
        [NonAction]
        private void MarcarFilasBorradasDataSet(List<FacetaFiltroHome> pListaFacetaFiltroHome, List<FacetaHome> pListaFacetaHome, bool pEliminarReciprocas = true)
        {
            foreach (FacetaFiltroHome filaFiltroHome in pListaFacetaFiltroHome)
            {
                GestorFacetas.FacetasDW.ListaFacetaFiltroHome.Remove(filaFiltroHome);
            }

            foreach (FacetaHome filaFiltroHome in pListaFacetaHome)
            {
                GestorFacetas.FacetasDW.ListaFacetaHome.Remove(filaFiltroHome);
            }
        }

        /// <summary>
        /// Comprueba si el dataSet contiene la configuración para la faceta de tags en una posición y la de de categorías en la otra posición.
        /// </summary>
        /// <param name="pDataSet">DataSet</param>
        /// <param name="pPosicion1">Posición del 1º elemento</param>
        /// <param name="pPosicion2">Posición del 2º elemento</param>
        /// <returns>TRUE si el dataSet contiene la configuración para la faceta de tags en una posición y la de de categorías en la otra posición, FALSE si no</returns>
        [NonAction]
        private bool ContieneFacetasTagsYCatEn(int pPosicion1, int pPosicion2)
        {
            if (GestorFacetas.ListaFacetas.Count > pPosicion2)
            {
                return ((GestorFacetas.ListaFacetas[pPosicion1].ClaveFaceta.Equals("sioc_t:Tag") && GestorFacetas.ListaFacetas[pPosicion2].ClaveFaceta.Equals("skos:ConceptID")) || ((GestorFacetas.ListaFacetas[pPosicion1].ClaveFaceta.Equals("skos:ConceptID") && GestorFacetas.ListaFacetas[pPosicion2].ClaveFaceta.Equals("sioc_t:Tag"))));
            }

            return false;
        }

        /// <summary>
        /// Comprueba si el dataSet contiene la configuración para la faceta de categorías en una posición.
        /// </summary>
        /// <param name="pDataSet">DataSet</param>
        /// <param name="pPosicion1">Posición del 1º elemento</param>
        /// <param name="pPosicion2">Posición del 2º elemento</param>
        /// <returns>TRUE si el dataSet contiene la configuración para la faceta de categorías en una posición, FALSE si no</returns>
        [NonAction]
        private bool ContieneFacetasCatYCatEn(int pPosicion1, int pPosicion2)
        {
            if (GestorFacetas.ListaFacetas.Count > pPosicion2)
            {
                return (GestorFacetas.ListaFacetas[pPosicion1].ClaveFaceta.Equals("skos:ConceptID") && GestorFacetas.ListaFacetas[pPosicion2].ClaveFaceta.Equals("skos:ConceptID"));
            }

            return false;
        }

        /// <summary>
        /// Comprueba si hay algún filtro de mes para una faceta de fechas
        /// </summary>
        /// <param name="pNombreFaceta">Nombre de la faceta</param>
        /// <param name="pListaFiltros">Filtros del usuario</param>
        /// <returns></returns>
        [NonAction]
        private bool ComprobarFiltroMeses(string pNombreFaceta, Dictionary<string, List<string>> pListaFiltros)
        {
            bool hayFiltroMeses = false;
            if (pListaFiltros.ContainsKey(pNombreFaceta) && pListaFiltros[pNombreFaceta].Count > 0)
            {
                char[] separadores = { '-' };
                foreach (string filtro in pListaFiltros[pNombreFaceta])
                {
                    string[] fechas = filtro.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                    if (fechas != null && fechas.Length > 0)
                    {
                        foreach (string fecha in fechas)
                        {
                            if (!string.IsNullOrEmpty(fecha) && fecha.Length > 5)
                            {
                                int mes = 0;
                                if (int.TryParse(fecha.Substring(4, 2), out mes) && mes > 0)
                                {
                                    //Hay un filtro de fecha, meto en el dataset el filtro para que se muestre la faceta
                                    mFacetadoDS.Tables.Add(pNombreFaceta);
                                    mFacetadoDS.Tables[pNombreFaceta].Columns.Add(pNombreFaceta.Replace(":", "") + "_2");
                                    mFacetadoDS.Tables[pNombreFaceta].Columns.Add("a", typeof(int));
                                    DataRow filaNueva = mFacetadoDS.Tables[pNombreFaceta].NewRow();
                                    filaNueva[0] = fecha.Substring(0, 6);
                                    filaNueva[1] = 0;
                                    mFacetadoDS.Tables[pNombreFaceta].Rows.Add(filaNueva);

                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return hayFiltroMeses;
        }

        private void ObtenerDeVirtuosoRangoMinMax(string pClaveFaceta, Dictionary<string, List<string>> pListaFiltros, Faceta pFaceta, bool pOmitirPalabrasNoRelevantesSearch, bool pPermitirRecursosPrivados, bool pInmutable, bool pEsMovil)
        {
            bool excluirPersonas = false;
            if ((TipoProyecto)FilaProyecto.TipoProyecto == TipoProyecto.Catalogo && !ParametrosGenerales.MostrarPersonasEnCatalogo)
            {
                excluirPersonas = true;
            }
            mFacetadoCL.ObtenerFaceta(mGrafoID, mFacetadoDS, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pFaceta.ElementosVisibles * 2, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, null, pFaceta.Excluyente, false, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, TipoPropiedadFaceta.FechaMinMax, FiltrosSearchPersonalizados, pInmutable, pEsMovil);
        }

        [NonAction]
        private void ObtenerDeVirtuosoRangoFechas(string pClaveFaceta, Dictionary<string, List<string>> pListaFiltros, Faceta pFaceta, bool pOmitirPalabrasNoRelevantesSearch, bool pPermitirRecursosPrivados, bool pInmutable, bool pEsMovil)
        {
            bool excluirPersonas = false;
            if ((TipoProyecto)FilaProyecto.TipoProyecto == TipoProyecto.Catalogo && !ParametrosGenerales.MostrarPersonasEnCatalogo)
            {
                excluirPersonas = true;
            }

            mFacetadoCL.ObtenerFaceta(mGrafoID, mFacetadoDS, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pFaceta.ElementosVisibles * 2, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, null, pFaceta.Excluyente, false, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);

            if (mFacetadoDS.Tables[pClaveFaceta].Rows.Count > 0)
            {
                if (mFacetadoDS.Tables[pClaveFaceta].Rows.Count == 1)
                {
                    //si hay solo un año, obtengo rangos por meses
                    ObtenerMeses(pClaveFaceta, pListaFiltros, pFaceta, pOmitirPalabrasNoRelevantesSearch, pPermitirRecursosPrivados, excluirPersonas, pInmutable, pEsMovil);
                }
            }
        }

        /// <summary>
        /// Obtiene los rangos de meses y los almacena en la variable mFacetadoDS
        /// </summary>
        /// <param name="pAnio"></param>
        /// <param name="pNombreFaceta"></param>
        /// <param name="pListaFiltros"></param>
        /// <param name="pFaceta"></param>
        /// <param name="pOmitirPalabrasNoRelevantesSearch"></param>
        /// <param name="pUsarHilos"></param>
        /// <param name="pPermitirRecursosPrivados"></param>
        /// <param name="pExcluirPersonas"></param>
        [NonAction]
        private void ObtenerRangoMeses(int pAnio, string pNombreFaceta, Dictionary<string, List<string>> pListaFiltros, Faceta pFaceta, bool pOmitirPalabrasNoRelevantesSearch, bool pUsarHilos, bool pPermitirRecursosPrivados, bool pExcluirPersonas, bool pEsMovil)
        {
            FacetadoDS facetadoDSAux = new FacetadoDS();
            List<int> rangos = new List<int>();

            //de enero a marzo
            rangos.Add(pAnio + 3);
            rangos.Add(pAnio + 1);

            mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSAux, pNombreFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pFaceta.ElementosVisibles * 2, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, pFaceta.Excluyente, pUsarHilos, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, false, pEsMovil);

            mFacetadoDS.Merge(facetadoDSAux);
            rangos.Clear();
            //de abril a junio
            rangos.Add(pAnio + 6);
            rangos.Add(pAnio + 4);

            mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSAux, pNombreFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pFaceta.ElementosVisibles * 2, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, pFaceta.Excluyente, pUsarHilos, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, false, pEsMovil);

            mFacetadoDS.Merge(facetadoDSAux);
            rangos.Clear();
            //de Julio a Septiembre
            rangos.Add(pAnio + 9);
            rangos.Add(pAnio + 7);

            mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSAux, pNombreFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pFaceta.ElementosVisibles * 2, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, pFaceta.Excluyente, pUsarHilos, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, false, pEsMovil);

            mFacetadoDS.Merge(facetadoDSAux);
            rangos.Clear();
            //de Octubre a Diciembre
            rangos.Add(pAnio + 12);
            rangos.Add(pAnio + 10);

            mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSAux, pNombreFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pFaceta.ElementosVisibles * 2, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, pFaceta.Excluyente, pUsarHilos, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, false, pEsMovil);

            mFacetadoDS.Merge(facetadoDSAux);
        }

        /// <summary>
        /// Obtiene los rangos de meses y los almacena en la variable mFacetadoDS
        /// </summary>
        /// <param name="pNombreFaceta"></param>
        /// <param name="pListaFiltros"></param>
        /// <param name="pFaceta"></param>
        /// <param name="pOmitirPalabrasNoRelevantesSearch"></param>
        /// <param name="pPermitirRecursosPrivados"></param>
        /// <param name="pExcluirPersonas"></param>
        [NonAction]
        private void ObtenerMeses(string pClaveFaceta, Dictionary<string, List<string>> pListaFiltros, Faceta pFaceta, bool pOmitirPalabrasNoRelevantesSearch, bool pPermitirRecursosPrivados, bool pExcluirPersonas, bool pInmutable, bool pEsMovil)
        {
            // Elimino el rango de años, porque sólo hay uno, y calculo el de los meses. 
            mFacetadoDS.Tables[pClaveFaceta].Clear();
            mFacetadoCL.ObtenerFaceta(mGrafoID, mFacetadoDS, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pFaceta.ElementosVisibles * 2, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, null, pFaceta.Excluyente, false, pExcluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, TipoPropiedadFaceta.FechaMeses, FiltrosSearchPersonalizados, pInmutable, pEsMovil);
        }

        [NonAction]
        private void ObtenerDeVirtuosoRangoSiglos(string pClaveFaceta, Dictionary<string, List<string>> pListaFiltros, Faceta pFaceta, bool pOmitirPalabrasNoRelevantesSearch, int pNumElementosVisibles, bool pPermitirRecursosPrivados, bool pInmutable, bool pEsMovil)
        {
            bool excluirPersonas = false;
            if ((TipoProyecto)FilaProyecto.TipoProyecto == TipoProyecto.Catalogo && !ParametrosGenerales.MostrarPersonasEnCatalogo)
            {
                excluirPersonas = true;
            }

            bool usarHilos = false;

            FacetadoDS facetadoDSRangos = new FacetadoDS();
            mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSRangos, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pNumElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, null, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);

            FacetadoDS facetadoDSAux = new FacetadoDS();
            List<int> rangos = new List<int>();

            //Ya hay un filtro por fecha, por lo que los resultados del DS son las décadas del siglo
            if (facetadoDSRangos.Tables[pClaveFaceta].Rows.Count >= 1 && pListaFiltros.ContainsKey(pClaveFaceta))
            {
                //Agrupamos por décadas
                List<int> listaAniosOrdenados = ObtenerListaAnios(facetadoDSRangos, pClaveFaceta, pListaFiltros[pClaveFaceta][0].StartsWith("-"));
                for (int i = 0; i < listaAniosOrdenados.Count; i++)
                {
                    rangos.Clear();
                    rangos.Add(listaAniosOrdenados[i] * 10);

                    rangos.Add((listaAniosOrdenados[i] + 1) * 10);

                    mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSAux, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pNumElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);

                    if (facetadoDSAux.Tables[pClaveFaceta].Rows.Count > 0)
                    {
                        int cantidad = int.Parse(facetadoDSAux.Tables[pClaveFaceta].Rows[0][1].ToString());

                        if (cantidad == 0 && !listaAniosOrdenados.Contains((listaAniosOrdenados[i] - 1)))
                        {
                            //No ha encontrado ningún elemento 
                            rangos.Clear();
                            rangos.Add(listaAniosOrdenados[i] * 10 - 1);

                            rangos.Add((listaAniosOrdenados[i] + 1) * 10);

                            mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSAux, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pNumElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);
                        }
                    }

                    mFacetadoDS.Merge(facetadoDSAux);
                }
            }
            else
            {
                //si hay 4 o menos, saco esos rangos
                List<int> listaAniosOrdenados = ObtenerListaAnios(facetadoDSRangos, pClaveFaceta, false);
                for (int i = 0; i < listaAniosOrdenados.Count; i++)
                {
                    int anio = listaAniosOrdenados[i];

                    rangos.Clear();
                    rangos.Add(anio + 1);
                    rangos.Add(anio);

                    mFacetadoCL.ObtenerFaceta(mGrafoID, facetadoDSAux, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pNumElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);

                    //Versión 1875 --> Duplicaba valores del DS como el año 2009, 2009, 2010... etc
                    //Fallaba en la versión 1875, funciona en la 1880.
                    mFacetadoDS.Merge(facetadoDSAux);
                }
            }
        }

        [NonAction]
        private List<int> ObtenerListaAnios(FacetadoDS pFacetadoDSRangos, string pNombreFaceta, bool pNegativo)
        {
            List<int> listaAniosOrdenados = new List<int>();
            foreach (DataRow anioDR in pFacetadoDSRangos.Tables[pNombreFaceta].Rows)
            {
                int anio = -1;
                if (int.TryParse(anioDR[0].ToString(), out anio))
                {
                    if (pNegativo)
                    {
                        anio--;
                    }
                    listaAniosOrdenados.Add(anio);
                }
            }

            listaAniosOrdenados.Sort();

            return listaAniosOrdenados;
        }

        [NonAction]
        private void ObtenerDeVirtuosoRangoCalendario(string pClaveFaceta, Faceta pFaceta, bool pPermitirRecursosPrivados, bool pInmutable, bool pEsMovil)
        {
            List<int> rangos = new List<int>();

            if (mListaFiltros.ContainsKey(pClaveFaceta))
            {
                string fechaActual = mListaFiltros[pClaveFaceta][0];
                if (mListaFiltros[pClaveFaceta].Count > 1)
                {
                    fechaActual = mListaFiltros[pClaveFaceta][mListaFiltros[pClaveFaceta].Count - 1];
                }

                if (fechaActual.Contains("-"))
                {
                    string[] delimiter = { "-" };
                    string[] fechas = fechaActual.Split(delimiter, StringSplitOptions.None);

                    if (!string.IsNullOrEmpty(fechas[0]))
                    {
                        int agnoMayor = int.Parse(fechas[0]) / 100;
                        int mesMayor = int.Parse(fechas[0]) - agnoMayor * 100;
                        rangos.Add((agnoMayor * 100 + mesMayor - 1) * 100);
                    }

                    if (!string.IsNullOrEmpty(fechas[1]))
                    {
                        int agnoMenor = int.Parse(fechaActual) / 100;
                        int mesMenor = int.Parse(fechaActual) - agnoMenor * 100;
                        rangos.Add((agnoMenor * 100 + mesMenor + 1) * 100 + DateTime.DaysInMonth(agnoMenor, mesMenor));
                    }
                }
                else
                {

                    int agno = int.Parse(fechaActual) / 100;
                    int mes = int.Parse(fechaActual) - agno * 100;

                    if (mes == 1)
                    {
                        rangos.Add(((agno - 1) * 100 + 12) * 100);
                        rangos.Add((agno * 100 + mes + 1) * 100 + DateTime.DaysInMonth(agno, mes));
                    }
                    else if (mes == 12)
                    {
                        rangos.Add((agno * 100 + mes - 1) * 100);
                        rangos.Add(((agno + 1) * 100 + 1) * 100 + DateTime.DaysInMonth(agno, mes));
                    }
                    else
                    {
                        rangos.Add((agno * 100 + mes - 1) * 100);
                        rangos.Add((agno * 100 + mes + 1) * 100 + DateTime.DaysInMonth(agno, mes));
                    }
                }
                mListaFiltros.Remove(pClaveFaceta);
            }

            mFacetadoCL.ObtenerFaceta(mGrafoID, mFacetadoDS, pClaveFaceta, mListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, 0, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, rangos, pFaceta.Excluyente, false, false, pPermitirRecursosPrivados, true, 0, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);
        }

        #region Rangos
        [NonAction]
        private void ObtenerDeVirtuosoRangoValores(string pClaveFaceta, Dictionary<string, List<string>> pListaFiltros, Faceta pFaceta, bool pOmitirPalabrasNoRelevantesSearch, int pNumElementosVisibles, bool pPermitirRecursosPrivados, bool pInmutable, bool pEsMovil)
        {
            bool excluirPersonas = false;
            if ((TipoProyecto)FilaProyecto.TipoProyecto == TipoProyecto.Catalogo && !ParametrosGenerales.MostrarPersonasEnCatalogo)
            {
                excluirPersonas = true;
            }

            bool usarHilos = false;

            FacetadoDS facetadoDSRangos = new FacetadoDS();
            mFacetadoCL.FacetadoCN.ObtenerContadoresRecursosAgrupadosParaFacetaRangos(mGrafoID, facetadoDSRangos, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pNumElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, null, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);

            Dictionary<string, double> dicValoresRango = ObtenerDiccionarioRangosDesdeDS(facetadoDSRangos, pClaveFaceta);
            Dictionary<string, double> dicValoresRangoAgrupados = new Dictionary<string, double>();
            Dictionary<string, double> dicValoresSubRango = new Dictionary<string, double>();

            if (dicValoresRango.Count > 0)
            {
                IEnumerable<KeyValuePair<string, double>> dicFilasSuperanValorPorcentaje = ObtenerFilasQSuperaValor(dicValoresRango, PORCENTAJE_APLICAR_TOTAL_RESULTADOS_CALCULO_RANGOS, true);

                FacetadoDS facetadoDSSubRangos = new FacetadoDS();
                if (dicFilasSuperanValorPorcentaje.Count() == 1)
                {
                    //2.1: Si solo hay uno, obtener rangos de ese y agruparlos
                    int numCifrasCantidad = dicFilasSuperanValorPorcentaje.First().Key.Split('-')[0].Length;

                    mFacetadoCL.FacetadoCN.ObtenerSubrangosDeCantidad(mGrafoID, facetadoDSSubRangos, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pNumElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, null, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, numCifrasCantidad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);

                    //obtenemos el multiplo de 10 por el que hay que multiplicar las cantidades para hacer los rangos
                    dicValoresSubRango = ObtenerDiccionarioSubRangosDesdeDS(facetadoDSSubRangos, pClaveFaceta, numCifrasCantidad);

                    //Agrupamos los resultados de 2 en 2.
                    dicValoresSubRango = AgruparColumnasConFormatoRango(dicValoresSubRango, numCifrasCantidad);

                }
                else if (dicFilasSuperanValorPorcentaje.Count() > 1)
                {
                    Dictionary<string, double> tempDic = new Dictionary<string, double>();
                    //2.2: Si hay más de uno obtener los valores y sacar las mitades
                    foreach (KeyValuePair<string, double> dr in dicFilasSuperanValorPorcentaje)
                    {
                        //2.1: Si solo hay uno, obtener rangos de ese y agruparlos
                        int numCifrasCantidad = dr.Key.Split('-')[0].Length;

                        facetadoDSSubRangos.Clear();

                        mFacetadoCL.FacetadoCN.ObtenerSubrangosDeCantidad(mGrafoID, facetadoDSSubRangos, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pNumElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, null, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, numCifrasCantidad, FiltrosSearchPersonalizados, pInmutable, pEsMovil);

                        tempDic = ObtenerDiccionarioSubRangosDesdeDS(facetadoDSSubRangos, pClaveFaceta, numCifrasCantidad);

                        tempDic = AgruparSubrangoPorMitades(tempDic, numCifrasCantidad);

                        foreach (string tempIndice in tempDic.Keys)
                        {
                            if (!dicValoresSubRango.ContainsKey(tempIndice))
                            {
                                dicValoresSubRango.Add(tempIndice, tempDic[tempIndice]);
                            }
                        }
                    }
                }

                dicValoresRangoAgrupados = AgruparFilasConMenosResultadosDelTotal(dicValoresRango, dicValoresSubRango, dicFilasSuperanValorPorcentaje);
            }

            mFacetadoDS.Tables.Add(ObtenerDSRangosFinal(pClaveFaceta, dicValoresRangoAgrupados));

            //facCN.Dispose();
        }
        [NonAction]
        private void ObtenerDeVirtuosoFacetaMultiple(string pClaveFaceta, Dictionary<string, List<string>> pListaFiltros, Faceta pFaceta, bool pOmitirPalabrasNoRelevantesSearch, int pNumElementosVisibles, bool pPermitirRecursosPrivados, bool pInmutable, bool pEsMovil)
        {
            bool excluirPersonas = false;
            if ((TipoProyecto)FilaProyecto.TipoProyecto == TipoProyecto.Catalogo && !ParametrosGenerales.MostrarPersonasEnCatalogo)
            {
                excluirPersonas = true;
            }

            bool usarHilos = false;

            FacetadoCN facCN = new FacetadoCN(mUtilServicios.UrlIntragnoss, mEntityContext, mLoggingService, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);

            if (mProyectoID != ProyectoAD.MetaProyecto)
            {
                facCN.ListaItemsBusquedaExtra = mListaItemsBusquedaExtra;
                facCN.InformacionOntologias = InformacionOntologias;
                facCN.PropiedadesRango = mUtilServiciosFacetas.ObtenerPropiedadesRango(GestorFacetas);
                facCN.PropiedadesFecha = mUtilServiciosFacetas.ObtenerPropiedadesFecha(GestorFacetas);
                facCN.ListaComunidadesPrivadasUsuario = new List<Guid>();
            }

            FacetaObjetoConocimientoProyecto filaFaceta = (FacetaObjetoConocimientoProyecto)pFaceta.FilaElementoEntity;

            //TODO: Migrar a EF
            //if (filaFaceta.GetFacetaMultipleRows().Length > 0)
            //{
            //    FacetaDS.FacetaMultipleRow filaFacetaMultiple = filaFaceta.GetFacetaMultipleRows()[0];

            //    string consulta = CompilarConsultaFacetaMultiple(filaFacetaMultiple);

            //    facCN.ObtenerFacetaMultiple(mGrafoID, mFacetadoDS, pClaveFaceta, pListaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pNumElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, true, null, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pEsMovil, consulta);

            //    if (mFacetadoDS.Tables[pClaveFaceta].Rows.Count > 0)
            //    {
            //        int max = filaFacetaMultiple.NumeroFacetasDesplegar;
            //        if (filaFacetaMultiple.NumeroFacetasDesplegar > mFacetadoDS.Tables[pClaveFaceta].Rows.Count)
            //        {
            //            max = mFacetadoDS.Tables[pClaveFaceta].Rows.Count;
            //        }
            //        for (int i = 0; i < max; i++)
            //        {
            //            Dictionary<string, List<string>> listaFiltros = new Dictionary<string, List<string>>(pListaFiltros);

            //            string filtro = filaFacetaMultiple.Filtro;
            //            string tipo = "";
            //            bool esRango = false;
            //            string facetaID = (string)mFacetadoDS.Tables[pClaveFaceta].Rows[i]["facetID"];
            //            listaFiltros.Add(filtro, new List<string>() { facetaID });

            //            if (mFacetadoDS.Tables[pClaveFaceta].Columns.Contains("facetType") && !mFacetadoDS.Tables[pClaveFaceta].Rows[i].IsNull("facetType"))
            //            {
            //                tipo = (string)mFacetadoDS.Tables[pClaveFaceta].Rows[i]["facetType"];
            //            }
            //            if (tipo.Equals("rango") || tipo.Equals("range"))
            //            {
            //                esRango = true;
            //            }

            //            FacetadoDS facetadoDS = new FacetadoDS();

            //            if (esRango)
            //            {
            //                FacetadoDS facetadoOriginal = mFacetadoDS;
            //                mFacetadoDS = facetadoDS;

            //                ObtenerDeVirtuosoRangoValores(pClaveFaceta, listaFiltros, pFaceta, pOmitirPalabrasNoRelevantesSearch, pNumElementosVisibles, pPermitirRecursosPrivados, pInmutable, pEsMovil);

            //                facetadoDS = mFacetadoDS;
            //                mFacetadoDS = facetadoOriginal;
            //            }
            //            else
            //            {
            //                facCN.ObtenerFaceta(mGrafoID, facetadoDS, pFaceta.ClaveFaceta, listaFiltros, mListaItemsBusquedaExtra, mEsMyGnoss, mEstaEnProyecto, mEsUsuarioInvitado, mIdentidadID.ToString(), pFaceta.TipoDisenio, 0, pFaceta.ElementosVisibles, mFormulariosSemanticos, mFiltroContextoWhere, (TipoProyecto)FilaProyecto.TipoProyecto, esRango, null, pFaceta.Excluyente, usarHilos, excluirPersonas, pPermitirRecursosPrivados, pOmitirPalabrasNoRelevantesSearch, pFaceta.Reciproca, pFaceta.TipoPropiedad, FiltrosSearchPersonalizados, pEsMovil);
            //            }

            //            facetadoDS.Tables[pFaceta.ClaveFaceta].TableName = $"{pFaceta.ClaveFaceta}_{facetaID}";

            //            mFacetadoDS.Merge(facetadoDS);
            //            facetadoDS.Dispose();
            //        }
            //    }

            //    facCN.Dispose();
            //}
        }

        [NonAction]
        private string CompilarConsultaFacetaMultiple(FacetaMultiple pFilaFacetaMultiple)
        {
            string consulta = $"{pFilaFacetaMultiple.Consulta} LIMIT {pFilaFacetaMultiple.NumeroFacetasObtener}";

            int indiceClausulaIF = consulta.IndexOf(CLAUSULA_IF);

            while (indiceClausulaIF >= 0)
            {
                int indiceClausulaThen = consulta.IndexOf(CLAUSULA_THEN);
                string clausulaIf = consulta.Substring(indiceClausulaIF + CLAUSULA_IF.Length, indiceClausulaThen - indiceClausulaIF - CLAUSULA_IF.Length);

                clausulaIf = clausulaIf.Trim('(', ' ');

                if (clausulaIf.StartsWith(CLAUSULA_EXIST_FILTER))
                {
                    string filtro = clausulaIf.Substring(CLAUSULA_EXIST_FILTER.Length).Trim(' ', '(', ')', '\"');

                    if (mListaFiltros.ContainsKey(filtro))
                    {
                        // El filtro existe, el contenido del if debe formar parte de la consulta
                        consulta = consulta.Remove(indiceClausulaIF, indiceClausulaThen + CLAUSULA_THEN.Length - indiceClausulaIF);
                        consulta = consulta.Remove(consulta.IndexOf(CLAUSULA_ENDIF), CLAUSULA_ENDIF.Length);
                    }
                    else
                    {
                        // El filtro NO existe, el contenido del if NO debe formar parte de la consulta
                        consulta = consulta.Remove(indiceClausulaIF, consulta.IndexOf(CLAUSULA_ENDIF) + CLAUSULA_ENDIF.Length - indiceClausulaIF);
                    }
                }

                indiceClausulaIF = consulta.IndexOf(CLAUSULA_IF);
            }

            return consulta;
        }

        private Dictionary<string, double> EliminarValoresHeObtenidoSubRango(Dictionary<string, double> pDicValoresRango, IEnumerable<KeyValuePair<string, double>> pDicFilasSuperanValorPorcentaje)
        {
            Dictionary<string, double> tempDic = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> valor in pDicFilasSuperanValorPorcentaje)
            {
                tempDic.Add(valor.Key, valor.Value);
            }

            foreach (string clave in tempDic.Keys)
            {
                pDicValoresRango.Remove(clave);
            }

            return pDicValoresRango;
        }

        private DataTable ObtenerDSRangosFinal(string pClaveFaceta, Dictionary<string, double> pDicValoresRangoAgrupados)
        {
            DataTable dt = new DataTable(pClaveFaceta);
            string nombrePrimeraColumna = pClaveFaceta.Replace(":", "") + "2";
            dt.Columns.Add(nombrePrimeraColumna);
            dt.Columns.Add("2");
            foreach (string rangoAgrupado in pDicValoresRangoAgrupados.Keys)
            {
                DataRow dr = dt.NewRow();
                dr[nombrePrimeraColumna] = rangoAgrupado;
                dr["2"] = pDicValoresRangoAgrupados[rangoAgrupado];
                dt.Rows.Add(dr);
            }

            return dt;
        }

        [NonAction]
        private Dictionary<string, double> ObtenerDiccionarioRangosDesdeDS(FacetadoDS facetadoDSRangos, string pClaveFaceta)
        {
            Dictionary<string, double> dicDesdeDS = new Dictionary<string, double>();

            foreach (DataRow dr in facetadoDSRangos.Tables[pClaveFaceta].Rows)
            {
                double indice;
                if (double.TryParse(dr[0].ToString(), out indice))
                {
                    double multiploDeDiez = ObtenerMultiploDeDiez(indice);

                    string cadenaIndice = "";
                    if (multiploDeDiez == 1)
                    {
                        cadenaIndice = "0-10";
                    }
                    else
                    {
                        cadenaIndice = (multiploDeDiez + "-" + (multiploDeDiez * 10)).ToString();
                    }

                    dicDesdeDS.Add(cadenaIndice, double.Parse(dr[1].ToString()));
                }
            }

            return dicDesdeDS;
        }

        private Dictionary<string, double> ObtenerDiccionarioSubRangosDesdeDS(FacetadoDS facetadoDSSubRangos, string pClaveFaceta, int pNumCifras)
        {
            double multiploDeDiez = ObtenerMultiploDeDiez(pNumCifras);

            Dictionary<string, double> dicDesdeDS = new Dictionary<string, double>();
            if (pNumCifras == 1)
            {
                foreach (DataRow dr in facetadoDSSubRangos.Tables[pClaveFaceta].Rows)
                {
                    dicDesdeDS.Add(dr[0].ToString(), double.Parse(dr[1].ToString()));
                }
            }
            else
            {
                foreach (DataRow dr in facetadoDSSubRangos.Tables[pClaveFaceta].Rows)
                {
                    string cadenaIndice = "";

                    if (dr[0].ToString().Contains("-"))
                    {
                        double indice1;
                        if (!string.IsNullOrEmpty(dr[0].ToString().Split('-')[0]) && double.TryParse(dr[0].ToString().Split('-')[0], out indice1))
                        {
                            cadenaIndice = (indice1 * multiploDeDiez).ToString();
                        }
                        cadenaIndice += "-";
                        double indice2;
                        if (!string.IsNullOrEmpty(dr[0].ToString().Split('-')[1]) && double.TryParse(dr[0].ToString().Split('-')[0], out indice2))
                        {
                            cadenaIndice = (indice2 * multiploDeDiez).ToString();
                        }
                    }
                    else
                    {
                        double indice = double.Parse(dr[0].ToString());
                        cadenaIndice = (indice * multiploDeDiez).ToString();
                    }

                    double cantidad = double.Parse(dr[1].ToString());

                    dicDesdeDS.Add(cadenaIndice, cantidad);
                }
            }

            return dicDesdeDS;
        }

        [NonAction]
        private Dictionary<string, double> AgruparFilasConMenosResultadosDelTotal(Dictionary<string, double> pDicRangos, Dictionary<string, double> pDicSubRangos, IEnumerable<KeyValuePair<string, double>> pDicRangosSuperanValor)
        {
            IEnumerable<KeyValuePair<string, double>> rowsNOSuperanValorPorcentajeMinimo = ObtenerFilasQSuperaValor(pDicRangos, PORCENTAJE_APLICAR_MINIMO_RESULTADOS_AGRUPAR, false);

            while (rowsNOSuperanValorPorcentajeMinimo.Count() > 0)
            {
                Dictionary<string, double> tempDicRangos = new Dictionary<string, double>();
                KeyValuePair<string, double> drRangosNoSuperanPorcentajeMinimo = new KeyValuePair<string, double>();

                if (drRangosNoSuperanPorcentajeMinimo.Key != null)
                {

                    for (int i = 0; i < pDicRangos.Count(); i++)
                    {
                        if (drRangosNoSuperanPorcentajeMinimo.Key == pDicRangos.ElementAt(i).Key)
                        {
                            KeyValuePair<string, double> previousDataRow = ObtenerElementoDeDiccionario(pDicRangos, i - 1);
                            KeyValuePair<string, double> nextDataRow = ObtenerElementoDeDiccionario(pDicRangos, i + 1);
                            KeyValuePair<string, double> resultadoAgrupado = AgrupoRangoConValoresMenoresAlTotal(drRangosNoSuperanPorcentajeMinimo, previousDataRow, nextDataRow, pDicSubRangos, pDicRangosSuperanValor);
                            tempDicRangos.Add(resultadoAgrupado.Key, resultadoAgrupado.Value);
                        }
                        else
                        {
                            tempDicRangos.Add(pDicRangos.ElementAt(i).Key, pDicRangos.ElementAt(i).Value);
                        }
                    }
                }
                else
                {
                    break;
                }

                pDicRangos = tempDicRangos;

                rowsNOSuperanValorPorcentajeMinimo = ObtenerFilasQSuperaValor(pDicRangos, PORCENTAJE_APLICAR_MINIMO_RESULTADOS_AGRUPAR, false);
            }

            pDicRangos = EliminarValoresHeObtenidoSubRango(pDicRangos, pDicRangosSuperanValor);

            pDicRangos = LimpiarRangosFusionadosDiccionario(pDicRangos);

            //Agregar los subrangos que no se hayan añadido.
            if (pDicSubRangos.Keys.Count > 0)
            {
                pDicRangos = FusionarSubRangosConRangosOriginales(pDicRangos, pDicSubRangos);
            }

            return pDicRangos;
        }

        [NonAction]
        private KeyValuePair<string, double> ObtenerElementoDeDiccionario(Dictionary<string, double> pDicRangos, int pPosicionElemento)
        {
            KeyValuePair<string, double> elementoDiccionario = new KeyValuePair<string, double>();
            if (pDicRangos.Keys.Count > pPosicionElemento && pPosicionElemento >= 0)
            {
                elementoDiccionario = pDicRangos.ElementAt(pPosicionElemento);
            }

            return elementoDiccionario;
        }

        [NonAction]
        private KeyValuePair<string, double> AgrupoRangoConValoresMenoresAlTotal(KeyValuePair<string, double> pDrRangosNoSuperanPorcentajeMinimo, KeyValuePair<string, double> pPreviousDataRow, KeyValuePair<string, double> pNextDataRow, Dictionary<string, double> pDicSubRangos, IEnumerable<KeyValuePair<string, double>> pDicRangosSuperanValor)
        {
            KeyValuePair<string, double> resultadoFinal = new KeyValuePair<string, double>();
            if (pNextDataRow.Key != null && pPreviousDataRow.Key != null)
            {
                //Se agrupa con el colindante que menos valores tiene.
                if (pPreviousDataRow.Value <= pNextDataRow.Value)
                {
                    pPreviousDataRow = ObtenerSubrangoRelacionado(true, pPreviousDataRow, pDicRangosSuperanValor, pDicSubRangos);
                    string indice = $"{pPreviousDataRow.Key}-{pDrRangosNoSuperanPorcentajeMinimo.Key}";
                    double sumaResultados = pPreviousDataRow.Value + pDrRangosNoSuperanPorcentajeMinimo.Value;
                    resultadoFinal = new KeyValuePair<string, double>(indice, sumaResultados);
                }
                else
                {
                    pNextDataRow = ObtenerSubrangoRelacionado(false, pNextDataRow, pDicRangosSuperanValor, pDicSubRangos);
                    string indice = $"{pNextDataRow.Key}-{pDrRangosNoSuperanPorcentajeMinimo.Key}";
                    double sumaResultados = pNextDataRow.Value + pDrRangosNoSuperanPorcentajeMinimo.Value;
                    resultadoFinal = new KeyValuePair<string, double>(indice, sumaResultados);
                }
            }
            else if (pPreviousDataRow.Key == null)
            {
                pNextDataRow = ObtenerSubrangoRelacionado(false, pNextDataRow, pDicRangosSuperanValor, pDicSubRangos);
                string indice = $"{pDrRangosNoSuperanPorcentajeMinimo.Key}-{pNextDataRow.Key}";
                double sumaResultados = pNextDataRow.Value + pDrRangosNoSuperanPorcentajeMinimo.Value;
                resultadoFinal = new KeyValuePair<string, double>(indice, sumaResultados);
            }
            else if (pNextDataRow.Key == null)
            {
                pPreviousDataRow = ObtenerSubrangoRelacionado(true, pPreviousDataRow, pDicRangosSuperanValor, pDicSubRangos);
                string indice = $"{pPreviousDataRow.Key}-{pDrRangosNoSuperanPorcentajeMinimo.Key}";
                double sumaResultados = pPreviousDataRow.Value + pDrRangosNoSuperanPorcentajeMinimo.Value;
                resultadoFinal = new KeyValuePair<string, double>(indice, sumaResultados);
            }

            return resultadoFinal;
        }

        [NonAction]
        private KeyValuePair<string, double> ObtenerSubrangoRelacionado(bool pMayor, KeyValuePair<string, double> pDataRow, IEnumerable<KeyValuePair<string, double>> pDicRangosSuperanValor, Dictionary<string, double> pDicSubRangos)
        {
            if (pDicRangosSuperanValor.Contains(pDataRow))
            {
                int mayor = -1;
                int menor = -1;
                ObtenerEnterosMayorMenorDelKeyDeKeyValuePair(pDataRow, out mayor, out menor);

                KeyValuePair<string, double> tempValuePair = new KeyValuePair<string, double>();
                for (int i = 0; i < pDicSubRangos.Count; i++)
                {
                    int mayorSubRango = -1;
                    int menorSubRango = -1;
                    ObtenerEnterosMayorMenorDelKeyDeKeyValuePair(pDicSubRangos.ElementAt(i), out mayorSubRango, out menorSubRango);

                    if (mayorSubRango == -1 && menorSubRango == -1)
                    {
                        tempValuePair = pDicSubRangos.ElementAt(i);
                    }
                    else
                    {
                        if (mayor == mayorSubRango && pMayor)
                        {
                            tempValuePair = pDicSubRangos.ElementAt(i);
                        }
                        else if (menor == menorSubRango && !pMayor)
                        {
                            tempValuePair = pDicSubRangos.ElementAt(i);
                        }
                    }
                }

                if (tempValuePair.Key != null)
                {
                    pDataRow = tempValuePair;
                    pDicSubRangos.Remove(tempValuePair.Key);
                }
            }

            return pDataRow;
        }

        [NonAction]
        private void ObtenerEnterosMayorMenorDelKeyDeKeyValuePair(KeyValuePair<string, double> pDataRow, out int mayor, out int menor)
        {
            string[] delimiter = { "-" };
            string[] values = pDataRow.Key.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

            mayor = -1;
            menor = -1;
            if (values.Length > 1)
            {
                menor = int.Parse(values[0]);
                mayor = int.Parse(values[1]);
            }
        }

        [NonAction]
        private Dictionary<string, double> LimpiarRangosFusionadosDiccionario(Dictionary<string, double> pDicRangos)
        {
            List<string> tempListConMasDeUnRango = new List<string>();
            Dictionary<string, double> finalRangos = new Dictionary<string, double>();

            foreach (string rango in pDicRangos.Keys)
            {
                string[] delimiter = { "-" };
                string[] rangoDividido = rango.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                List<double> rangosEnterosDivididos = new List<double>();
                foreach (string rangod in rangoDividido)
                {
                    double rangoDint = double.Parse(rangod);
                    if (!rangosEnterosDivididos.Contains(rangoDint))
                    {
                        rangosEnterosDivididos.Add(rangoDint);
                    }
                }

                if (rango.Split('-').Length > 2)
                {
                    double menorValorRango = rangosEnterosDivididos.Min();
                    double mayorValorRango = rangosEnterosDivididos.Max();
                    finalRangos.Add($"{menorValorRango}-{mayorValorRango}", pDicRangos[rango]);
                }
                else
                {
                    finalRangos.Add(rango, pDicRangos[rango]);
                }
            }

            return finalRangos;
        }

        [NonAction]
        private Dictionary<string, double> FusionarSubRangosConRangosOriginales(Dictionary<string, double> pDicRangos, Dictionary<string, double> pDicSubRangos)
        {
            //Recorrer los diccionarios y unir los valores
            Dictionary<string, double> tempDicRangos = new Dictionary<string, double>();
            foreach (string rango in pDicRangos.Keys)
            {
                tempDicRangos.Add(rango, pDicRangos[rango]);
            }

            foreach (string rango in pDicSubRangos.Keys)
            {
                tempDicRangos.Add(rango, pDicSubRangos[rango]);
            }

            var items = from pair in tempDicRangos
                        orderby pair.Key.Length ascending
                        select pair;

            Dictionary<string, double> finalDicRangos = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> pair in items)
            {
                finalDicRangos.Add(pair.Key, pair.Value);
            }

            return finalDicRangos;
        }

        [NonAction]
        private Dictionary<string, double> AgruparSubrangoPorMitades(Dictionary<string, double> pDicSubRangos, int pCantidadNumeros)
        {
            double mitad = 0;
            double multiploDeDiez = ObtenerMultiploDeDiez(pCantidadNumeros);
            mitad = (multiploDeDiez * 10) / 2;

            string primero = "";
            double ultimo = -1;
            double suma = 0;
            Dictionary<string, double> dicSubrangos = new Dictionary<string, double>();

            #region Primera Mitad

            ObtenerValoresFiltroSubRango(pDicSubRangos, true, mitad, out primero, out ultimo, out suma);
            double ultimoTemp = ultimo;
            ultimoTemp += multiploDeDiez;

            if (ultimoTemp.ToString().Length > pCantidadNumeros)
            {
                ultimoTemp = multiploDeDiez * 10;
            }


            string indiceAgrupado = $"{primero}-{ultimoTemp}";
            if (primero != ultimoTemp.ToString())
            {
                if (!dicSubrangos.ContainsKey(indiceAgrupado))
                {
                    dicSubrangos.Add(indiceAgrupado, suma);
                }
            }
            else
            {
                if (!dicSubrangos.ContainsKey(primero))
                {
                    dicSubrangos.Add(primero, suma);
                }
            }

            #endregion Primera Mitad

            #region Segunda Mitad

            ObtenerValoresFiltroSubRango(pDicSubRangos, false, mitad, out primero, out ultimo, out suma);

            if (ultimo > 0)
            {
                //Hay que sumarle el multiplo de 10 ya que en el diccionario el último elemento es el 90, 900, 9000...
                ultimo = ultimo + multiploDeDiez;

                string indiceAgrupado2 = primero + "-" + ultimo;
                if (primero != ultimo.ToString())
                {
                    if (!dicSubrangos.ContainsKey(indiceAgrupado2))
                    {
                        dicSubrangos.Add(indiceAgrupado2, suma);
                    }
                }
                else
                {
                    if (!dicSubrangos.ContainsKey(primero))
                    {
                        dicSubrangos.Add(primero, suma);
                    }
                }
            }

            #endregion Segunda Mitad

            return dicSubrangos;
        }

        [NonAction]
        private void ObtenerValoresFiltroSubRango(Dictionary<string, double> pDicSubRangos, bool pMenor, double pMitad, out string primero, out double ultimo, out double suma)
        {
            primero = "";
            ultimo = -1;
            suma = 0;
            foreach (string indice in pDicSubRangos.Keys)
            {
                double indiceDouble = 0.0;
                if (double.TryParse(indice, out indiceDouble))
                {
                    if (pMenor)
                    {
                        if (indiceDouble < pMitad)
                        {
                            if (string.IsNullOrEmpty(primero))
                            {
                                primero = indice;
                            }

                            if (indiceDouble > ultimo)
                            {
                                ultimo = indiceDouble;
                            }

                            suma += pDicSubRangos[indice];
                        }
                    }
                    else
                    {
                        if (indiceDouble >= pMitad)
                        {
                            if (string.IsNullOrEmpty(primero))
                            {
                                primero = indice;
                            }

                            if (indiceDouble > ultimo)
                            {
                                ultimo = indiceDouble;
                            }

                            suma += pDicSubRangos[indice];
                        }
                    }
                }
            }
        }

        [NonAction]
        private Dictionary<string, double> AgruparColumnasConFormatoRango(IEnumerable<KeyValuePair<string, double>> pDicSubRangos, int pNumCifrasCantidad)
        {
            Dictionary<string, double> dicValoresAgrupados = new Dictionary<string, double>();

            double multiploDeDiez = ObtenerMultiploDeDiez(pNumCifrasCantidad);

            if (pDicSubRangos.Count() > 5)
            {
                dicValoresAgrupados = AgruparSubrangosCuandoHayMasDeCinco(pDicSubRangos, multiploDeDiez);
            }
            else
            {
                if (pNumCifrasCantidad == 1)
                {
                    //Mostramos los valores extrictos
                    foreach (KeyValuePair<string, double> row in pDicSubRangos)
                    {
                        dicValoresAgrupados.Add(row.Key, row.Value);
                    }
                }
                else
                {
                    dicValoresAgrupados = AgruparSubrangosCuandoHayMenosDeCinco(pDicSubRangos, multiploDeDiez, pNumCifrasCantidad);
                }
            }

            return dicValoresAgrupados;
        }

        [NonAction]
        private Dictionary<string, double> AgruparSubrangosCuandoHayMenosDeCinco(IEnumerable<KeyValuePair<string, double>> pDicSubRangos, double pMultiploDeDiez, int pNumCifrasCantidad)
        {
            Dictionary<string, double> dicValoresAgrupados = new Dictionary<string, double>();

            KeyValuePair<string, double> dicOrigen = new KeyValuePair<string, double>();
            for (int x = 0; x < pDicSubRangos.Count(); x++)
            {
                if (dicOrigen.Key == null)
                {
                    dicOrigen = pDicSubRangos.ElementAt(x);
                }
                else
                {
                    KeyValuePair<string, double> dr = pDicSubRangos.ElementAt(x);

                    double segundoValorTemp = double.Parse(dr.Key);
                    segundoValorTemp += pMultiploDeDiez;

                    if (segundoValorTemp.ToString().Length > pNumCifrasCantidad)
                    {
                        segundoValorTemp = pMultiploDeDiez * 10;
                    }

                    dicValoresAgrupados.Add(ObtenerRangoValores(double.Parse(dicOrigen.Key), segundoValorTemp), (dicOrigen.Value + dr.Value));
                    dicOrigen = new KeyValuePair<string, double>();
                }
            }

            if (pDicSubRangos.Count() % 2 == 1)
            {
                double tempIndice = -1;
                if (double.TryParse(dicOrigen.Key, out tempIndice))
                {
                    //Obtengo rango de 9 a 9
                    double fin = pMultiploDeDiez * 10;
                    double cantidad = dicOrigen.Value;
                    dicValoresAgrupados.Add(ObtenerRangoValores(tempIndice, fin), cantidad);
                }
            }

            return dicValoresAgrupados;
        }

        [NonAction]
        private Dictionary<string, double> AgruparSubrangosCuandoHayMasDeCinco(IEnumerable<KeyValuePair<string, double>> pDicSubRangos, double pMultiploDeDiez)
        {

            Dictionary<string, double> dicValoresAgrupados = new Dictionary<string, double>();
            KeyValuePair<string, double> dicOrigen = new KeyValuePair<string, double>();
            for (int x = 0; x < pDicSubRangos.Count(); x++)
            {
                if (dicOrigen.Key == null)
                {
                    dicOrigen = pDicSubRangos.ElementAt(x);
                }
                else
                {
                    KeyValuePair<string, double> dr = pDicSubRangos.ElementAt(x);
                    double inicio = double.Parse(dicOrigen.Key);
                    double fin = double.Parse(dr.Key) + pMultiploDeDiez;
                    double cantidad = dicOrigen.Value + dr.Value;
                    dicValoresAgrupados.Add(ObtenerRangoValores(inicio, fin), cantidad);
                    dicOrigen = new KeyValuePair<string, double>();
                }
            }

            if (pDicSubRangos.Count() % 2 == 1)
            {
                double tempIndice = -1;
                if (double.TryParse(dicOrigen.Key, out tempIndice))
                {
                    //Obtengo rango de 9 a 9
                    double fin = pMultiploDeDiez * 10;
                    double cantidad = dicOrigen.Value;
                    dicValoresAgrupados.Add(ObtenerRangoValores(tempIndice, fin), cantidad);
                }
            }

            return dicValoresAgrupados;
        }

        [NonAction]
        private double ObtenerMultiploDeDiez(double pCantidadNum)
        {
            double multiploDeDiez = 1;
            for (double i = 1; i < pCantidadNum; i++)
            {
                multiploDeDiez = multiploDeDiez * 10;
            }

            return multiploDeDiez;
        }

        [NonAction]
        private string ObtenerRangoValores(double pPrimerValorDataRow, double pSegundoValorDataRow)
        {
            string valor = "";
            if (pPrimerValorDataRow != -1)
            {
                valor = (pPrimerValorDataRow).ToString();
            }

            if (pSegundoValorDataRow != -1)
            {
                valor += "-";
                valor += (pSegundoValorDataRow).ToString();
            }

            return valor;
        }

        /// <summary>
        /// Método que devuelve una enumeración de filas a partir de un data set, el nombre de la tabla, el nombre de la columna y el porcentaje en el que basarse para devolver las filas.
        /// </summary>
        /// <param name="pFacetadoDSRangos">DataSet con los datos</param>
        /// <param name="pClaveFaceta">Nombre de la tabla</param>
        /// <param name="pNumColumna">Número de la columna</param>
        /// <param name="pPorcentajeCalculoRangos">Porcentaje usado para calcular el valor a partir del cual devolveremos las filas que lo superen</param>
        /// <returns>Filas que en la columna pNumColumna superen el valor obtenido del porcentaje calculado a partir del total de la suma de valores de la columna pNumColumna.</returns>
        [NonAction]
        private IEnumerable<KeyValuePair<string, double>> ObtenerFilasQSuperaValor(Dictionary<string, double> pDicFilasTotal, decimal pPorcentajeCalculoRangos, bool pMayor)
        {
            decimal decimalTemporal = 0;
            decimal numTotalRecursosAgrupados = pDicFilasTotal.AsEnumerable().Where(r => decimal.TryParse(r.Value.ToString(), out decimalTemporal)).Sum(r => decimalTemporal);

            decimal porcentajeDelTotalParaAgrupar = numTotalRecursosAgrupados * pPorcentajeCalculoRangos;

            IEnumerable<KeyValuePair<string, double>> filasCumplenCondicion;
            if (pMayor)
            {
                filasCumplenCondicion = pDicFilasTotal.AsEnumerable().Where(r2 => decimal.TryParse(r2.Value.ToString(), out decimalTemporal) && decimalTemporal >= porcentajeDelTotalParaAgrupar);
            }
            else
            {
                filasCumplenCondicion = pDicFilasTotal.AsEnumerable().Where(r2 => decimal.TryParse(r2.Value.ToString(), out decimalTemporal) && decimalTemporal <= porcentajeDelTotalParaAgrupar);
            }

            return filasCumplenCondicion;
        }

        #endregion Rangos

        /// <summary>
        /// Ordena los elementos de la faceta, de tal manera que se pueda indentar su contenido
        /// </summary>
        /// <param name="pClaveFaceta">Clave de la faceta a ordenar</param>
        /// <param name="pElementosFaceta">Elementos que contine la faceta</param>
        /// <param name="pParametrosElementos">Parametros de los elementos de la faceta</param>
        /// <returns>Un diccionario con las claves de cada elemento y como valor la lista de sus hijos</returns>
        [NonAction]
        public Dictionary<string, List<string>> OrdenarElementosFaceta(string pClaveFaceta, Dictionary<string, int> pElementosFaceta, Dictionary<string, string> pParametrosElementos)
        {
            Dictionary<string, List<string>> listaElementosOrdenados = new Dictionary<string, List<string>>();

            switch (pClaveFaceta)
            {
                case "rdf:type":
                    //Por ahora solo se ordena la faceta tipo
                    string comunidadEducativa = GetText("CONFIGURACIONFACETADO", "COMUNIDADEDUCATIVA");
                    string comunidadNoEducativa = GetText("CONFIGURACIONFACETADO", "COMUNIDADNOEDUCATIVA");

                    string claseUni = GetText("CONFIGURACIONFACETADO", "CLASEUNI");
                    string claseESO = GetText("CONFIGURACIONFACETADO", "CLASESEC");
                    string clase = GetText("CONFIGURACIONFACETADO", "CLASE");
                    string organizaciones = GetText("COMMON", "ORGANIZACIONES");
                    string personas = GetText("COMMON", "PERSONAS");
                    string comunidades = GetText("COMMON", "COMUNIDADES");

                    string contribucionesEncuesta = GetText("CONFIGURACIONFACETADO", "CONTENCUESTA");
                    string contribucionesFD = GetText("CONFIGURACIONFACETADO", "CONTCOMFACTORDAFO");
                    string contribucionesDebates = GetText("CONFIGURACIONFACETADO", "CONTDEBATE");
                    string contribucionesPreguntas = GetText("CONFIGURACIONFACETADO", "CONTPREGUNTA");
                    string contribucionesRecComp = GetText("CONFIGURACIONFACETADO", "CONTRECCOMP");
                    string contribucionesRecPub = GetText("CONFIGURACIONFACETADO", "CONTRECPUB");
                    string contactos = GetText("CONTACTOS", "CONTACTOS"); //contactos grupos

                    if (((pElementosFaceta.ContainsKey(comunidadEducativa)) || (pElementosFaceta.ContainsKey(comunidadNoEducativa))) && ((!this.mListaFiltrosFacetasUsuario.ContainsKey(comunidadEducativa)) && (!mListaFiltrosFacetasUsuario.ContainsKey(comunidadNoEducativa))))
                    {
                        //Comunidades

                        if (this.mListaFiltrosFacetasUsuario.ContainsKey("rdf:type") && ((this.mListaFiltrosFacetasUsuario["rdf:type"].Contains(FacetadoAD.BUSQUEDA_COMUNIDAD_EDUCATIVA)) || (mListaFiltrosFacetasUsuario["rdf:type"].Contains(FacetadoAD.BUSQUEDA_COMUNIDAD_NO_EDUCATIVA))))
                        {
                            //Si el usuario ha hecho un filtro de algo específico, no se ordena la faceta
                            goto default;
                        }

                        if (!pElementosFaceta.ContainsKey(comunidades))
                        {
                            //Primero el item comunidades
                            pElementosFaceta.Add(comunidades, 0);
                            pParametrosElementos.Add(comunidades, FacetadoAD.BUSQUEDA_COMUNIDADES);
                        }

                        listaElementosOrdenados.Add(comunidades, new List<string>());

                        //Debajo de comunidades, comunidades educativas y no
                        if (pElementosFaceta.ContainsKey(comunidadEducativa))
                        {

                            listaElementosOrdenados[comunidades].Add(comunidadEducativa);

                            pElementosFaceta[comunidades] += pElementosFaceta[comunidadEducativa];
                        }

                        if (pElementosFaceta.ContainsKey(comunidadNoEducativa))
                        {
                            listaElementosOrdenados[comunidades].Add(comunidadNoEducativa);

                            pElementosFaceta[comunidades] += pElementosFaceta[comunidadNoEducativa];
                        }
                    }

                    if (mTipoBusqueda.Equals(TipoBusqueda.Contactos) && ((pElementosFaceta.ContainsKey(personas)) || (pElementosFaceta.ContainsKey(organizaciones))))
                    {
                        if (!pElementosFaceta.ContainsKey(contactos))
                        {
                            //después el item Contactos
                            pElementosFaceta.Add(contactos, 0);
                        }
                        else
                        {
                            pElementosFaceta[contactos] = 0;
                        }

                        listaElementosOrdenados.Add(contactos, new List<string>());

                        //Y debajo de Contactos, personas o org:
                        if (pElementosFaceta.ContainsKey(personas))
                        {
                            listaElementosOrdenados[contactos].Add(personas);

                            pElementosFaceta[contactos] += pElementosFaceta[personas];
                        }

                        if (pElementosFaceta.ContainsKey(organizaciones))
                        {
                            listaElementosOrdenados[contactos].Add(organizaciones);

                            pElementosFaceta[contactos] += pElementosFaceta[organizaciones];
                        }
                    }
                    if ((pElementosFaceta.ContainsKey(claseESO)) || (pElementosFaceta.ContainsKey(claseUni)))
                    {
                        //Personas y organizaciones
                        if (mListaFiltrosFacetasUsuario.ContainsKey("rdf:type") && ((this.mListaFiltrosFacetasUsuario["rdf:type"].Contains(FacetadoAD.BUSQUEDA_CLASE_SECUNDARIA)) || (mListaFiltrosFacetasUsuario["rdf:type"].Contains(FacetadoAD.BUSQUEDA_CLASE_UNIVERSIDAD))))
                        {
                            //Si el usuario ha hecho un filtro de algo específico, no se ordena la faceta
                            goto default;
                        }

                        if (pElementosFaceta.ContainsKey(personas))
                        {
                            //Primero el item personas
                            listaElementosOrdenados.Add(personas, new List<string>());
                        }

                        if (pElementosFaceta.ContainsKey(organizaciones))
                        {
                            //luego el item organizaciones
                            listaElementosOrdenados.Add(organizaciones, new List<string>());
                        }

                        if (!pElementosFaceta.ContainsKey(clase))
                        {
                            //después el item clase
                            pElementosFaceta.Add(clase, 0);
                            pParametrosElementos.Add(clase, FacetadoAD.BUSQUEDA_CLASE);
                        }

                        listaElementosOrdenados.Add(clase, new List<string>());

                        //Y debajo de clase, clase ESO o UNI
                        if (pElementosFaceta.ContainsKey(claseESO))
                        {
                            listaElementosOrdenados[clase].Add(claseESO);

                            pElementosFaceta[clase] += pElementosFaceta[claseESO];
                        }

                        if (pElementosFaceta.ContainsKey(claseUni))
                        {
                            listaElementosOrdenados[clase].Add(claseUni);

                            pElementosFaceta[clase] += pElementosFaceta[claseUni];
                        }
                    }

                    if (pElementosFaceta.ContainsKey(contribucionesRecPub) || pElementosFaceta.ContainsKey(contribucionesRecComp))
                    {
                        if (mListaFiltrosFacetasUsuario.ContainsKey("rdf:type") && (mListaFiltrosFacetasUsuario["rdf:type"].Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPARTIDO) || mListaFiltrosFacetasUsuario["rdf:type"].Contains(FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PUBLICADO)))
                        {
                            goto default;
                        }

                        if (pElementosFaceta.ContainsKey(contribucionesPreguntas))
                        {
                            listaElementosOrdenados.Add(contribucionesPreguntas, new List<string>());
                            if (!pParametrosElementos.ContainsKey(contribucionesPreguntas))
                                pParametrosElementos.Add(contribucionesPreguntas, FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PREGUNTA);

                        }

                        if (pElementosFaceta.ContainsKey(contribucionesEncuesta))
                        {
                            listaElementosOrdenados.Add(contribucionesEncuesta, new List<string>());
                            if (!pParametrosElementos.ContainsKey(contribucionesEncuesta))
                                pParametrosElementos.Add(contribucionesEncuesta, FacetadoAD.BUSQUEDA_CONTRIBUCIONES_ENCUESTA);

                        }

                        if (pElementosFaceta.ContainsKey(contribucionesFD))
                        {
                            listaElementosOrdenados.Add(contribucionesFD, new List<string>());
                            if (!pParametrosElementos.ContainsKey(contribucionesFD))
                                pParametrosElementos.Add(contribucionesFD, FacetadoAD.BUSQUEDA_CONTRIBUCIONES_FACTORDAFO);

                        }

                        if (pElementosFaceta.ContainsKey(contribucionesDebates))
                        {
                            listaElementosOrdenados.Add(contribucionesDebates, new List<string>());
                            if (!pParametrosElementos.ContainsKey(contribucionesDebates))
                                pParametrosElementos.Add(contribucionesDebates, FacetadoAD.BUSQUEDA_CONTRIBUCIONES_DEBATE);

                        }

                        if (pElementosFaceta.ContainsKey(contribucionesRecComp))
                        {
                            listaElementosOrdenados.Add(contribucionesRecComp, new List<string>());
                            if (!pParametrosElementos.ContainsKey(contribucionesRecComp))
                                pParametrosElementos.Add(contribucionesRecComp, FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPARTIDO);
                        }

                        if (pElementosFaceta.ContainsKey(contribucionesRecPub))
                        {
                            listaElementosOrdenados.Add(contribucionesRecPub, new List<string>());
                            if (!pParametrosElementos.ContainsKey(contribucionesRecPub))
                                pParametrosElementos.Add(contribucionesRecPub, FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PUBLICADO);
                        }
                    }

                    //El resto de tipos se quedan como vienen
                    foreach (string key in pElementosFaceta.Keys)
                    {
                        if ((!key.Equals(comunidadEducativa)) && (!key.Equals(comunidadNoEducativa)) && (!key.Equals(claseUni)) && (!key.Equals(contribucionesRecComp)) && (!key.Equals(contribucionesRecPub)) && (!key.Equals(claseESO)) && (!listaElementosOrdenados.ContainsKey(key)))
                        {
                            listaElementosOrdenados.Add(key, new List<string>());
                        }
                    }

                    break;
                default:
                    //El orden es el que traigan de virtuoso.
                    foreach (string key in pElementosFaceta.Keys)
                    {
                        listaElementosOrdenados.Add(key, new List<string>());
                    }
                    break;
            }

            return listaElementosOrdenados;
        }

        #region Obtener nombres reales

        /// <summary>
        /// Obtiene el nombre real de un filtro determinado
        /// </summary>
        /// <param name="pFiltrosFacetas">Lista de filtros</param>
        /// <param name="pClaveFaceta">Clave de la faceta</param>
        /// <param name="pFiltro">Filtro de esa faceta</param>
        /// <returns></returns>
        [NonAction]
        private string ObtenerNombreRealFiltro(List<string> pFiltrosFacetas, string pClaveFaceta, string pFiltro, TipoPropiedadFaceta pTipoPropiedad)
        {
            mLoggingService.AgregarEntrada("ObtenerNombreRealFiltro: Inicio");

            string nombreReal = pFiltro;

            if (pTipoPropiedad.Equals(TipoPropiedadFaceta.Fecha))
            {
                string fecha1 = "";
                string fecha2 = "";
                string valor = pFiltro;

                int indiceFecha2 = 1;

                char[] separadorFecha = { '/', '-' };
                string[] cachosFecha = valor.Split(separadorFecha, StringSplitOptions.RemoveEmptyEntries);
                bool esAnio = false;
                if (cachosFecha.Length < 2)
                {
                    //estaba separado por '/'
                    string fecha = cachosFecha[0];

                    string dia = fecha.Substring(6, 2);
                    string mes = fecha.Substring(4, 2);
                    string anio = fecha.Substring(0, 4);

                    valor = anio + dia + mes;

                    if (dia != "00")
                    {
                        fecha1 += $"{dia}/";
                    }

                    if (mes != "00")
                    {
                        fecha1 += $"{mes}/";
                    }

                    if (anio != "0000")
                    {
                        fecha1 += anio + "/";
                    }

                    if (fecha1.EndsWith("/"))
                    {
                        fecha1 = fecha1.Substring(0, fecha1.Length - 1);
                    }

                    if (pFiltro.StartsWith("-"))
                    {
                        valor = $"-{valor}";
                        fecha1 = $"{GetText("COMBUSQUEDAAVANZADA", "ANTERIORA")} {fecha1}";
                    }
                    else
                    {
                        int agno = 0;
                        if (fecha1.Length == 4 && int.TryParse(fecha1, out agno))
                        {
                            fecha1 = agno.ToString();
                        }
                        valor += "-";
                    }
                }
                else
                {
                    if (cachosFecha.Length == 1)
                    {
                        if (pFiltro.StartsWith("-"))
                        {
                            fecha1 = GetText("COMBUSQUEDAAVANZADA", "ANTERIORA") + " ";
                        }
                        else
                        {
                            fecha1 = GetText("COMBUSQUEDAAVANZADA", "POSTERIORA") + " ";
                        }
                        indiceFecha2 = 0;
                    }
                    else
                    {
                        fecha1 = cachosFecha[0];

                        string dia = fecha1.Substring(6, 2);
                        string mes = fecha1.Substring(4, 2);
                        string anio = fecha1.Substring(0, 4);

                        fecha2 = cachosFecha[1];

                        string dia2 = fecha2.Substring(6, 2);
                        string mes2 = fecha2.Substring(4, 2);
                        string anio2 = fecha2.Substring(0, 4);

                        if (dia.Equals("00"))
                        {
                            if (!mes.Equals("00"))
                            {
                                int numeroMes = 0;
                                int.TryParse(mes, out numeroMes);
                                fecha1 = ObtenerTextoRangoFecha(false, numeroMes);
                                fecha2 = " " + GetText("COMBUSQUEDAAVANZADA", "DEL") + " " + anio;
                                esAnio = true;
                            }
                            else
                            {
                                fecha2 = anio;
                                fecha1 = "";
                                esAnio = true;
                            }

                            if (!mes2.Equals("00"))
                            {
                                int numeroMes = 0;
                                int.TryParse(mes2, out numeroMes);
                                string mesSuperiorRango = ObtenerTextoRangoFecha(false, numeroMes);
                                if (!fecha1.Contains(mesSuperiorRango))
                                {
                                    fecha1 += $"-{mesSuperiorRango}";
                                }
                            }
                            else if (dia.Equals("00"))
                            {
                                //Solo tenemos el año
                                fecha1 = anio;
                                fecha2 = "-" + anio2;
                            }
                        }
                        else
                        {
                            fecha1 = dia + "/" + mes + "/" + anio + "-";
                        }
                    }

                    if (!esAnio && !string.IsNullOrEmpty(fecha2))
                    {
                        fecha2 = cachosFecha[indiceFecha2];
                        string diaFecha2 = fecha2.Substring(6, 2);
                        if (diaFecha2.Equals("00"))
                        {
                            diaFecha2 = "";
                        }
                        else
                        {
                            diaFecha2 += "/";
                        }

                        string mesFecha2 = fecha2.Substring(4, 2);
                        if (mesFecha2.Equals("00"))
                        {
                            mesFecha2 = "";
                        }
                        else
                        {
                            mesFecha2 += "/";
                        }

                        fecha2 = diaFecha2 + mesFecha2 + fecha2.Substring(0, 4);
                    }
                }

                nombreReal = fecha1 + fecha2;
                

                if (!pFiltro.Equals(valor))
                {
                    int indiceFiltro = pFiltrosFacetas.IndexOf(pFiltro);
                    if (indiceFiltro != -1 && pFiltrosFacetas.Contains(pFiltro))
                    {
                        pFiltrosFacetas.Remove(pFiltro);
                        pFiltrosFacetas.Insert(indiceFiltro, valor);
                    }
                }
            }
            else if (pTipoPropiedad.Equals(TipoPropiedadFaceta.Siglo))
            {
                nombreReal = ObtenerNombreRealFiltroSiglo(pFiltro, pFiltrosFacetas);
            }
            else
            {

                switch (pClaveFaceta)
                {
                    case "skos:ConceptID":
                        Guid id = mUtilServiciosFacetas.ObtenerIDDesdeURI(pFiltro);
                        if (GestorTesauro.ListaCategoriasTesauro.ContainsKey(id))
                        {
                            nombreReal = GestorTesauro.ListaCategoriasTesauro[id].Nombre[UtilIdiomas.LanguageCode];
                        }
                        break;
                    case "sioc:has_space":
                    case "gnoss:hasComunidadOrigen":
                        if (mTipoBusqueda != TipoBusqueda.Suscripciones)
                        {
                            if (mTipoBusqueda == TipoBusqueda.RecomendacionesProys)
                            {
                                Guid idIdentidad = mUtilServiciosFacetas.ObtenerIDDesdeURI(pFiltro);

                                IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                                List<Guid> lista = new List<Guid>();
                                lista.Add(idIdentidad);
                            }
                            else
                            {
                                Guid idproy = mUtilServiciosFacetas.ObtenerIDDesdeURI(pFiltro);

                                if (idproy.Equals(ProyectoAD.MetaProyecto))
                                {
                                    nombreReal = GetText("CONFIGURACIONFACETADO", "ESPACIOSPERSONALES");
                                }
                                else
                                {
                                    ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                                    nombreReal = proyectoCL.ObtenerFilaProyecto(idproy).Nombre;
                                }
                            }
                        }
                        break;
                    case "gnoss:hasnivelcertification"://nivel
                        if (pFiltro.Equals("100"))
                        {
                            nombreReal = GetText("COMBUSQUEDAAVANZADA", "NOCERTIFI");
                        }
                        else
                        {
                            nombreReal = NivelesCertificacionDW.ListaNivelCertificacion.FirstOrDefault(nivel => nivel.Orden.ToString().Equals(pFiltro)).Descripcion;
                        }
                        break;
                    case "gnoss:userstatus":
                        if (pFiltro.Equals("1"))
                        {
                            nombreReal = GetText("ESTADOSUSUARIO", "ACTIVO");
                        }
                        else if (pFiltro.Equals("2"))
                        {
                            nombreReal = GetText("ESTADOSUSUARIO", "BLOQUEADO");
                        }
                        else if (pFiltro.Equals("3"))
                        {
                            nombreReal = GetText("ESTADOSUSUARIO", "EXPULSADO");
                        }
                        break;
                    case "gnoss:rol":
                        if (pFiltro.Equals("0"))
                        {
                            nombreReal = GetText("ROLESUSUARIO", "ADMINISTRADOR");
                        }
                        else if (pFiltro.Equals("1"))
                        {
                            nombreReal = GetText("ROLESUSUARIO", "SUPERVISOR");
                        }
                        else if (pFiltro.Equals("2"))
                        {
                            nombreReal = GetText("ROLESUSUARIO", "USUARIO");
                        }
                        break;
                    case "gnoss:hastipodoc"://tipo
                        int tipoDoc = 0;
                        int.TryParse(pFiltro, out tipoDoc);
                        nombreReal = ObtenerNombreTipoDocumento(tipoDoc);
                        break;
                    case "gnoss:hastipodocExt"://tipo
                        int tipoExt;
                        bool result = int.TryParse(pFiltro, out tipoExt);
                        if (result)
                            nombreReal = ObtenerNombreTipoDocumento(tipoExt);
                        break;
                    case "rdf:type"://tipo
                        nombreReal = ObtenerNombreTipoElemento(pFiltro);
                        break;
                    case "gnoss:hasnumeroVotos":
                    case "gnoss:hasnumeroComentarios":
                    case "gnoss:hasnumeroVisitas":
                        if (pFiltro.Contains("-"))
                        {
                            if (pFiltro.EndsWith("-"))
                            {
                                //Se buscan los valores mayores que un valor
                                nombreReal = GetText("COMBUSQUEDAAVANZADA", "MAYORDE") + " " + pFiltro.Remove(pFiltro.Length - 1);
                            }
                            else if (pFiltro.StartsWith("-"))
                            {
                                //Se buscan los valores mayores que un valor
                                nombreReal = GetText("COMBUSQUEDAAVANZADA", "MENORDE") + " " + pFiltro.Substring(1);
                            }
                            else
                            {
                                //Se buscan los valores entre un rango
                                char[] separador = { '-' };
                                string[] rango = pFiltro.Split(separador, StringSplitOptions.RemoveEmptyEntries);
                                nombreReal = GetText("COMBUSQUEDAAVANZADA", "DE") + " " + rango[0] + " " + GetText("COMBUSQUEDAAVANZADA", "A") + " " + rango[1];
                            }
                        }
                        else
                        {
                            //Se busca el valor exacto de un pFiltro con rango
                            nombreReal = pFiltro;
                        }
                        break;
                    case "gnoss:hasEstadoCorreccion":
                        switch (pFiltro)
                        {
                            case "1":
                                nombreReal = GetText("COMBUSQUEDAAVANZADA", "NOTIFICADONOCAMBIADO");
                                break;
                            case "2":
                                nombreReal = GetText("COMBUSQUEDAAVANZADA", "NOTIFICADOCAMBIADO");
                                break;
                            case "3":
                                nombreReal = GetText("COMBUSQUEDAAVANZADA", "NOTIFICADONOCAMBIADO3DIAS");
                                break;
                        }
                        break;
                    case "documentoid":
                        DocumentacionCN docCN = new DocumentacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                        nombreReal = docCN.ObtenerTituloDocumentoPorID(new Guid(pFiltro));
                        docCN.Dispose();
                        break;
                    default:
                        if (string.IsNullOrEmpty(pFiltro) || pFiltro.Equals(FacetadoAD.FILTRO_SIN_ESPECIFICAR))
                        {
                            nombreReal = TextoSinEspecificar;
                        }
                        break;
                }
            }

            mLoggingService.AgregarEntrada("ObtenerNombreRealFiltro: Fin");

            return nombreReal;
        }

        [NonAction]
        private string ObtenerDecadaDelSiglo(string pFecha)
        {
            int anioInicio = ObtenerAnioFechaSinUltimaCifra(pFecha);
            string decada;

            int decadaInferiorAnio;
            int decadaSuperiorAnio;
            if (anioInicio < 0 || pFecha.StartsWith("-"))
            {
                decadaInferiorAnio = anioInicio * 10;
                decadaSuperiorAnio = (anioInicio * 10) - 9;

                decada = $"{-decadaSuperiorAnio}-{-decadaInferiorAnio} a.C.";
            }
            else
            {
                decadaInferiorAnio = (anioInicio - 1) * 10;
                decadaSuperiorAnio = ((anioInicio - 1) * 10) + 9;

                decada = $"{decadaInferiorAnio}-{decadaSuperiorAnio}";
            }

            return decada;
        }

        [NonAction]
        private string ObtenerNombreRealFiltroSiglo(string pFiltro, List<string> pFiltrosFacetas)
        {
            string nombreReal = "";
            string siglo1 = "";

            char[] separadorFecha = { '/', '-' };
            string[] cachosFecha = pFiltro.Split(separadorFecha, StringSplitOptions.RemoveEmptyEntries);

            if (pFiltro.StartsWith("-"))
            {
                List<string> cachoFechaTemporal = new List<string>();
                //Siglo negativo, Antes de Cristo
                foreach (string cachofecha in cachosFecha)
                {
                    cachoFechaTemporal.Add($"-{cachofecha}");
                }

                cachosFecha = cachoFechaTemporal.ToArray();
            }

            if (cachosFecha.Length > 1)
            {
                if ((pFiltrosFacetas.Count > 0 && pFiltrosFacetas[0] == pFiltro) || pFiltrosFacetas.Count == 0)
                {
                    // Siglo
                    siglo1 = cachosFecha[0];
                    int numero = 0;
                    if (siglo1.Length == 8)
                    {
                        if (int.Parse(siglo1) < 0)
                        {
                            numero = int.Parse(siglo1.Substring(0, 2));
                        }
                        else
                        {
                            numero = int.Parse(siglo1.Substring(0, 2)) + 1;
                        }
                    }
                    else if (siglo1.Length == 7)
                    {
                        if (int.Parse(siglo1) < 0)
                        {
                            numero = int.Parse(siglo1.Substring(0, 1)) - 1;
                        }
                        else
                        {
                            numero = int.Parse(siglo1.Substring(0, 1)) + 1;
                        }
                    }

                    string numeroRomano = ObtenerSigloNumeroRomano(numero);
                    nombreReal = GetText("COMBUSQUEDAAVANZADA", "SIGLO") + " " + numeroRomano;
                }
                else
                {
                    // Década
                    nombreReal = ObtenerDecadaDelSiglo(cachosFecha[1]);
                }
            }
            else
            {
                //Obtener el año
                nombreReal = ObtenerAnioFecha(cachosFecha[0]);
            }

            return nombreReal;
        }

        [NonAction]
        private string ObtenerSigloNumeroRomano(int pNumero)
        {
            if (mDicNumerosRomanos == null || mDicNumerosRomanos.Keys.Count == 0)
            {
                mDicNumerosRomanos = new Dictionary<int, string>();
                CargarDicNumerosRomanos();
            }

            string numeroRomano = pNumero.ToString();
            if (mDicNumerosRomanos.ContainsKey(pNumero))
            {
                numeroRomano = mDicNumerosRomanos[pNumero];
            }

            return numeroRomano;
        }

        [NonAction]
        private void CargarDicNumerosRomanos()
        {
            mDicNumerosRomanos.Add(-1, "I a.C.");
            mDicNumerosRomanos.Add(-2, "II a.C.");
            mDicNumerosRomanos.Add(-3, "III a.C.");
            mDicNumerosRomanos.Add(-4, "IV a.C.");
            mDicNumerosRomanos.Add(-5, "V a.C.");
            mDicNumerosRomanos.Add(-6, "VI a.C.");
            mDicNumerosRomanos.Add(-7, "VII a.C.");
            mDicNumerosRomanos.Add(-8, "VIII a.C.");
            mDicNumerosRomanos.Add(-9, "IX a.C.");
            mDicNumerosRomanos.Add(-10, "X a.C.");
            mDicNumerosRomanos.Add(-11, "XI a.C.");
            mDicNumerosRomanos.Add(-12, "XII a.C.");
            mDicNumerosRomanos.Add(-13, "XIII a.C.");
            mDicNumerosRomanos.Add(-14, "XIV a.C.");
            mDicNumerosRomanos.Add(-15, "XV a.C.");
            mDicNumerosRomanos.Add(-16, "XVI a.C.");
            mDicNumerosRomanos.Add(-17, "XVII a.C.");
            mDicNumerosRomanos.Add(-18, "XVIII a.C.");
            mDicNumerosRomanos.Add(-19, "XIX a.C.");
            mDicNumerosRomanos.Add(-20, "XX a.C.");
            mDicNumerosRomanos.Add(-21, "XXI a.C.");
            mDicNumerosRomanos.Add(-22, "XXII a.C.");
            mDicNumerosRomanos.Add(-23, "XXIII a.C.");
            mDicNumerosRomanos.Add(-24, "XXIV a.C.");
            mDicNumerosRomanos.Add(-25, "XXV a.C.");
            mDicNumerosRomanos.Add(-26, "XXVI a.C.");
            mDicNumerosRomanos.Add(-27, "XXVII a.C.");
            mDicNumerosRomanos.Add(-28, "XXVIII a.C.");
            mDicNumerosRomanos.Add(-29, "XXIX a.C.");
            mDicNumerosRomanos.Add(-30, "XXX a.C.");

            mDicNumerosRomanos.Add(1, "I");
            mDicNumerosRomanos.Add(2, "II");
            mDicNumerosRomanos.Add(3, "III");
            mDicNumerosRomanos.Add(4, "IV");
            mDicNumerosRomanos.Add(5, "V");
            mDicNumerosRomanos.Add(6, "VI");
            mDicNumerosRomanos.Add(7, "VII");
            mDicNumerosRomanos.Add(8, "VIII");
            mDicNumerosRomanos.Add(9, "IX");
            mDicNumerosRomanos.Add(10, "X");
            mDicNumerosRomanos.Add(11, "XI");
            mDicNumerosRomanos.Add(12, "XII");
            mDicNumerosRomanos.Add(13, "XIII");
            mDicNumerosRomanos.Add(14, "XIV");
            mDicNumerosRomanos.Add(15, "XV");
            mDicNumerosRomanos.Add(16, "XVI");
            mDicNumerosRomanos.Add(17, "XVII");
            mDicNumerosRomanos.Add(18, "XVIII");
            mDicNumerosRomanos.Add(19, "XIX");
            mDicNumerosRomanos.Add(20, "XX");
            mDicNumerosRomanos.Add(21, "XXI");
            mDicNumerosRomanos.Add(22, "XXII");
            mDicNumerosRomanos.Add(23, "XXIII");
            mDicNumerosRomanos.Add(24, "XXIV");
            mDicNumerosRomanos.Add(25, "XXV");
            mDicNumerosRomanos.Add(26, "XXVI");
            mDicNumerosRomanos.Add(27, "XXVII");
            mDicNumerosRomanos.Add(28, "XXVIII");
            mDicNumerosRomanos.Add(29, "XXIX");
            mDicNumerosRomanos.Add(30, "XXX");
        }

        [NonAction]
        private int ObtenerAnioFechaSinUltimaCifra(string pFecha)
        {
            int anioInicio;
            if (pFecha.Length == 8)
            {
                anioInicio = int.Parse(pFecha.Substring(0, 3));
            }
            else if (pFecha.Length == 7)
            {
                anioInicio = int.Parse(pFecha.Substring(0, 2));
            }
            else if (pFecha.Length == 6)
            {
                anioInicio = int.Parse(pFecha.Substring(0, 2));
                if (anioInicio % 10 == 0)
                {
                    if (!pFecha.StartsWith("-"))
                    {
                        anioInicio = int.Parse(pFecha.Substring(0, 1));
                    }
                    else
                    {
                        anioInicio = int.Parse(pFecha.Substring(1, 2));
                    }
                }
            }
            else
            {
                anioInicio = int.Parse(pFecha.Substring(0, 2));
                if (anioInicio % 10 == 0)
                {
                    if (!pFecha.StartsWith("-"))
                    {
                        anioInicio = int.Parse(pFecha.Substring(0, 1));
                    }
                    else
                    {
                        anioInicio = int.Parse(pFecha.Substring(1, 2));
                    }
                }
            }

            return anioInicio;
        }

        [NonAction]
        public string ObtenerAnioFecha(string pFecha)
        {
            string anioString = "";
            int anio = 0;
            if (pFecha.StartsWith("-"))
            {
                switch (pFecha.Length)
                {
                    case 9:
                        anio = int.Parse(pFecha.Substring(0, 5));
                        break;
                    case 8:
                        anio = int.Parse(pFecha.Substring(0, 4));
                        break;
                    case 7:
                        anio = int.Parse(pFecha.Substring(0, 3));
                        break;
                    case 6:
                        anio = int.Parse(pFecha.Substring(0, 2));
                        break;
                }

                anioString = $"{anio.ToString()} a.C.";
            }
            else
            {
                switch (pFecha.Length)
                {
                    case 8:
                        anio = int.Parse(pFecha.Substring(0, 4));
                        break;
                    case 7:
                        anio = int.Parse(pFecha.Substring(0, 3));
                        break;
                    case 6:
                        anio = int.Parse(pFecha.Substring(0, 2));
                        break;
                    case 5:
                        anio = int.Parse(pFecha.Substring(0, 1));
                        break;
                }

                anioString = anio.ToString();
            }

            return anioString;
        }

        /// <summary>
        /// Obtiene el nombre de un tipo de documento concreto
        /// </summary>
        /// <param name="pTipo">Tipo de documento</param>
        /// <returns></returns>
        [NonAction]
        private string ObtenerNombreTipoDocumento(int pTipo)
        {
            string tipo = "";
            switch (pTipo)
            {
                case 0:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCHIPERVINCULO");
                    break;

                case 1:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCREFDOC");
                    break;

                case 2:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCVIDEO");
                    break;

                case 3:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCARCHIVODIG");
                    break;

                case 4:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCIMGWIKI");
                    break;

                case 5:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCFICHEROSEM");
                    break;

                case 6:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCIMAGEN");
                    break;

                case 7:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCONTOLOGIA");
                    break;

                case 8:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCNOTA");
                    break;

                case 9:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCWIKI");
                    break;

                case 10:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCENTRADABLOG");
                    break;

                case 11:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCNEWSLETTER");
                    break;

                case 12:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCARTIWIKITEMP");
                    break;

                case 13:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCENTRADABLOGTEMP");
                    break;

                case 14:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCDAFO");
                    break;

                case 15:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCPREGUNTA");
                    break;

                case 16:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCDEBATE");
                    break;

                case 17:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCBLOG");
                    break;
                case 18:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCENCUESTA");
                    break;
                case 21:
                    tipo = GetText("COMBUSQUEDAAVANZADA", "TIPODOCAUDIO");
                    break;
            }
            return tipo;
        }

        /// <summary>
        /// Obtiene el nombre de un tipo de item concreto
        /// </summary>
        /// <param name="pTipo">Tipo de item</param>
        /// <returns></returns>
        [NonAction]
        private string ObtenerNombreTipoElemento(string pTipo)
        {
            string tipo = "";
            switch (pTipo)
            {
                case FacetadoAD.BUSQUEDA_RECURSOS:
                    tipo = GetText("COMMON", "RECURSOS");
                    break;
                case FacetadoAD.BUSQUEDA_PERSONA:
                    tipo = GetText("COMMON", "PERSONAS");
                    break;
                case FacetadoAD.BUSQUEDA_GRUPO:
                    tipo = GetText("GRUPO", "GRUPOS");
                    break;
                case FacetadoAD.BUSQUEDA_PROFESOR:
                    tipo = GetText("CONFIGURACIONFACETADO", "PROFESOR");
                    break;
                case FacetadoAD.BUSQUEDA_PREGUNTAS:
                    tipo = GetText("COMMON", "PREGUNTAS");
                    break;
                case FacetadoAD.BUSQUEDA_ENCUESTAS:
                    tipo = GetText("COMMON", "ENCUESTAS");
                    break;
                case "Dafos":
                case FacetadoAD.BUSQUEDA_DAFOS:
                    tipo = GetText("COMMON", "DAFOS");
                    break;
                case FacetadoAD.BUSQUEDA_ORGANIZACION:
                    tipo = GetText("COMMON", "ORGANIZACIONES");
                    break;
                case FacetadoAD.BUSQUEDA_DEBATES:
                    tipo = GetText("COMMON", "DEBATES");
                    break;
                case FacetadoAD.BUSQUEDA_COMUNIDADES:
                    tipo = GetText("COMMON", "COMUNIDADES");
                    break;
                case FacetadoAD.BUSQUEDA_BLOGS:
                    tipo = GetText("COMMON", "BLOGS");
                    break;
                case FacetadoAD.BUSQUEDA_COMUNIDAD_EDUCATIVA:
                    tipo = GetText("CONFIGURACIONFACETADO", "COMUNIDADEDUCATIVA");
                    break;
                case FacetadoAD.BUSQUEDA_COMUNIDAD_NO_EDUCATIVA:
                    tipo = GetText("CONFIGURACIONFACETADO", "COMUNIDADNOEDUCATIVA");
                    break;
                case FacetadoAD.BUSQUEDA_CLASE:
                    tipo = GetText("CONFIGURACIONFACETADO", "CLASE");
                    break;
                case FacetadoAD.BUSQUEDA_CLASE_SECUNDARIA:
                    tipo = GetText("CONFIGURACIONFACETADO", "CLASESEC");
                    break;
                case FacetadoAD.BUSQUEDA_CLASE_UNIVERSIDAD:
                    tipo = GetText("CONFIGURACIONFACETADO", "CLASEUNI");
                    break;
                case FacetadoAD.BUSQUEDA_ALUMNO:
                    tipo = GetText("USUARIOS", "ALUMNOS");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_DEBATE:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTDEBATE");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_ENCUESTA:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTENCUESTA");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PREGUNTA:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTPREGUNTA");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_FACTORDAFO:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTCOMFACTORDAFO");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_RECURSOS:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTREC");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPARTIDO:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTRECCOMP");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_PUBLICADO:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTRECPUB");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENTARIOS:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTCOMR");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMRECURSOS:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTCOMREC");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMPREGUNTAS:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTCOMPRE");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMDEBATES:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTCOMDEB");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMENCUESTAS:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTCOMEMC");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMFACTORDAFO:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTCOMFD");
                    break;
                case FacetadoAD.BUSQUEDA_CONTRIBUCIONES_COMARTICULOBLOG:
                    tipo = GetText("CONFIGURACIONFACETADO", "CONTCOMAB");
                    break;
                case FacetadoAD.BUSQUEDA_ARTICULOSBLOG:
                    tipo = GetText("METABUSCADOR", "BUSCARARTICULOS");
                    break;
                case FacetadoAD.BUSQUEDA_CONTACTOS:
                    tipo = GetText("CONTACTOS", "CONTACTOS");
                    break;
                case FacetadoAD.BUSQUEDA_CONTACTOS_PERSONAL:
                    tipo = GetText("CONTACTOS", "PERSONAS");
                    break;
                case FacetadoAD.BUSQUEDA_CONTACTOS_ORGANIZACION:
                    tipo = GetText("COMMON", "ORGANIZACIONES");
                    break;
                case FacetadoAD.BUSQUEDA_CONTACTOS_GRUPO:
                    tipo = GetText("CONTACTOS", "GRUPOS");
                    break;
                case FacetadoAD.PAGINA_CMS:
                    tipo = GetText("COMMON", "PAGINACMS");
                    break;
                default:

                    tipo = pTipo;

                    List<OntologiaProyecto> filasOntologia = GestorFacetas.FacetasDW.ListaOntologiaProyecto.Where(item => item.OntologiaProyecto1.Equals(pTipo)).ToList();
                    if (filasOntologia.Count > 0)
                    {
                        tipo = filasOntologia[0].NombreOnt;
                    }
                    break;
            }

            return tipo;
        }

        /// <summary>
        /// Obtiene el nombre real de una faceta
        /// </summary>
        /// <param name="pNombres">Nombres de la faceta en todos sus idiomas (Ej: Localidad@es|||Location@en)</param>
        /// <returns></returns>
        [NonAction]
        public string ObtenerNombreFaceta(string pNombres)
        {
            return UtilCadenas.ObtenerTextoDeIdioma(pNombres, mLanguageCode, ParametrosGenerales.IdiomaDefecto);
        }


        /// <summary>
        /// Carga una identidad cuya clave se pasa por parametro
        /// </summary>
        /// <param name="pIdentidad">Identificador de la identidad que se desea cargar</param>
        /// <returns>Devuelve la identidad cuya clave se pasa por parametro</returns>
        [NonAction]
        private void CargarIdentidad(Guid pIdentidad)
        {
            IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            DataWrapperIdentidad dataWrapperIdentidad = identidadCN.ObtenerIdentidadPorIDCargaLigeraTablas(pIdentidad);
            identidadCN.Dispose();

            DataWrapperPersona dataWrapperPersona = new DataWrapperPersona();

            DataWrapperOrganizacion organizacionDS = new DataWrapperOrganizacion();

            new UtilServicioResultados(mLoggingService, mEntityContext, mConfigService, mRedisCacheWrapper, mVirtuosoAD, mServicesUtilVirtuosoAndReplication).ObtenerPersonasYOrgDeIdentidades(dataWrapperIdentidad, dataWrapperPersona, organizacionDS, true);

            GestionIdentidades gestorIdentidades = new GestionIdentidades(dataWrapperIdentidad, new GestionPersonas(dataWrapperPersona, mLoggingService, mEntityContext), new GestionOrganizaciones(organizacionDS, mLoggingService, mEntityContext), mLoggingService, mEntityContext, mConfigService, mServicesUtilVirtuosoAndReplication);

            mIdentidadActual = gestorIdentidades.ListaIdentidades[pIdentidad];
        }

        /// <summary>
        /// Obtiene el contenido de la etiqueta pasada como parámetro
        /// </summary>
        /// <param name="pPage">Etiqueta de la página en el fichero de idioma </param>
        /// <param name="pText">Etiqueta del texto en el fichero de idioma</param>
        /// <returns>Cadena de texto con el contenido de la etiqueta solicitada</returns>
        [NonAction]
        private string GetText(string pPage, string pText)
        {
            return UtilIdiomas.GetText(pPage, pText);
        }

        #endregion

        #region Fechas

        /// <summary>
        /// Obtiene el texto de un rango de fecha
        /// </summary>
        /// <param name="pAgrupadoPorAños">Verdad si está agrupado por años (falso si solo hay un año en el rango)</param>
        /// <param name="pFraccionFecha">Fracción de la fecha que corresponde a un rango (Ej: 201105 --> Desde mayo de 2005)</param>
        /// <returns></returns>
        private string ObtenerTextoRangoFecha(bool pAgrupadoPorAños, int pFraccionFecha)
        {
            string textoRango = pFraccionFecha.ToString();

            //Si está agrupado por años, el rango será el año
            if (!pAgrupadoPorAños)
            {
                //Si no está agrupado por años, el rango será el mes
                switch (pFraccionFecha)
                {
                    case 1:
                        textoRango = GetText("CONTROLESCVSEM", "MES_ENE");
                        break;
                    case 2:
                        textoRango = GetText("CONTROLESCVSEM", "MES_FEB");
                        break;
                    case 3:
                        textoRango = GetText("CONTROLESCVSEM", "MES_MAR");
                        break;
                    case 4:
                        textoRango = GetText("CONTROLESCVSEM", "MES_ABR");
                        break;
                    case 5:
                        textoRango = GetText("CONTROLESCVSEM", "MES_MAY");
                        break;
                    case 6:
                        textoRango = GetText("CONTROLESCVSEM", "MES_JUN");
                        break;
                    case 7:
                        textoRango = GetText("CONTROLESCVSEM", "MES_JUL");
                        break;
                    case 8:
                        textoRango = GetText("CONTROLESCVSEM", "MES_AGO");
                        break;
                    case 9:
                        textoRango = GetText("CONTROLESCVSEM", "MES_SEP");
                        break;
                    case 10:
                        textoRango = GetText("CONTROLESCVSEM", "MES_OCT");
                        break;
                    case 11:
                        textoRango = GetText("CONTROLESCVSEM", "MES_NOV");
                        break;
                    case 12:
                        textoRango = GetText("CONTROLESCVSEM", "MES_DIC");
                        break;
                    default:
                        //Si no ha entrado en ningún case, seguramente el més sea superior a 12.
                        textoRango = GetText("CONTROLESCVSEM", "MES_DIC");
                        break;
                }
            }
            return textoRango;
        }

        /// <summary>
        /// Coge una fecha en formato 01/01/2010 y lo cambia a 20100101000000
        /// </summary>
        /// <param name="pFecha">Fecha a convertir (en formato 01/01/2010)</param>
        /// <returns></returns>
        private string ConvertirFormatoFecha(string pFecha)
        {
            string nfecha;
            nfecha = pFecha.Substring(pFecha.LastIndexOf("/") + 1);
            pFecha = pFecha.Substring(0, pFecha.LastIndexOf("/"));
            nfecha += pFecha.Substring(pFecha.LastIndexOf("/") + 1);
            pFecha = pFecha.Substring(0, pFecha.LastIndexOf("/"));
            nfecha += pFecha.Substring(pFecha.LastIndexOf("/") + 1);

            return $"{nfecha}000000";
        }

        #endregion

        #region Carga de facetas

        /// <summary>
        /// Carga una faceta concreta
        /// </summary>
        /// <param name="pLimite">Límite</param>
        /// <param name="pLimiteOriginal">Límite original (si el limite es -1)</param>
        /// <param name="pFaceta">Faceta a cargar</param>
        /// <param name="pListaFacetas">Lista de facetas a la que añadir el modelo</param>
        /// <param name="pOneFacetRequest">Verdad si es una petición para cargar una sola faceta</param>
        /// <param name="pPlegadas">Verdad si la faceta debe mostrarse plegada</param>
        [NonAction]
        private void CargarFacetaDinamica(int pLimite, int pLimiteOriginal, Faceta pFaceta, List<FacetModel> pListaFacetas, bool? pPlegadas = null, bool? pOneFacetRequest = null)
        {
            FacetModel facetaModel = null;

            if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.TipoDoc))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaTipoDoc(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Tipo))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaTipoElemento(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Estado))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaEstado(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Rangos))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaRangos(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.CodPost))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaCodPost(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Categoria))
            {
                //TODO ALVARO
                if (!mFacetasHomeCatalogo)
                {
                    facetaModel = CargarFacetaCategoria(pLimite, pFaceta);
                }
                else
                {
                    mLoggingService.AgregarEntrada("CargarFacetaDinamica: Inicio de ¿Tiene Foto?");

                    bool tieneImagenes = false;
                    if (GestorTesauro.TesauroDW.ListaCategoriaTesauro.Where(item => item.TieneFoto == true).Count() > 0)
                    {
                        tieneImagenes = true;
                    }

                    mLoggingService.AgregarEntrada("CargarFacetaDinamica: Fin de ¿Tiene Foto?");

                    if (tieneImagenes)
                    {
                        facetaModel = CargarFacetaCategoriaHomeCatalogo(pLimite, false, pFaceta);
                    }
                    else
                    {
                        facetaModel = CargarFacetaCategoriaArbol(pLimite, pFaceta);
                    }
                }
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.CategoriaArbol))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaCategoriaArbol(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.ComunidadesRecomendadas))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaComunidadesRecomendadas(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Com2))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaCom2(pLimite, pLimiteOriginal, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.ComContactos))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaComContactos(pLimite, pLimiteOriginal, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.TipoContactos))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaTipoContactos(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.EstadoCorreccion))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaEstadoCorreccion(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.NCer))
            {
                if (ParametrosGenerales.PermitirCertificacionRec)
                {
                    //TODO ALVARO
                    facetaModel = CargarFacetaNCer(pLimite, pFaceta.Nombre, pFaceta);
                }
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Fechas))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaFechaDinamica(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.FechaMinMax))
            {
                facetaModel = CargarFacetaFechaMinMax(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Siglo))
            {
                facetaModel = CargarFacetaRangosSiglosDinamica(pLimite, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.TipoDocExt))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaTipoDocExt(pLimite, pFaceta);
            }
            else if (pFaceta != null && (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.TesauroSemantico) || pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado)) && (!(pFaceta.FilaElementoEntity is FacetaFiltroProyecto || pFaceta.FilaElementoEntity is FacetaFiltroHome) || pFaceta.FiltroProyectoID.Split(';')[0].Contains("-") || pFaceta.FiltroProyectoID.Split(';')[0].Trim() == ""))
            {
                //TODO ALVARO
                facetaModel = CargarFacetaTesauroSemantico(pLimite, pFaceta, pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado));
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.EstadoUsuario))
            {
                facetaModel = CargarFacetaEstadoUsuario(pLimite, pFaceta.Nombre, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.RolUsuario))
            {
                facetaModel = CargarFacetaRolUsuario(pLimite, pFaceta.Nombre, pFaceta);
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Multiple))
            {
                CargarFacetaMultiple(pLimite, pFaceta, pListaFacetas, pPlegadas, pOneFacetRequest);
            }
            else
            {
                //TODO ALVARO  
                //Carga la faceta sin algoritmo de transformación
                Dictionary<string, int> elementos = new Dictionary<string, int>();
                bool mostrarfaceta = true;
                if (mTipoBusqueda == TipoBusqueda.Contribuciones && pFaceta.ClaveFaceta.Equals("gnoss:haspublicador") && mListaFiltros.ContainsKey("gnoss:haspublicadorIdentidadID"))
                {
                    mostrarfaceta = false;
                }
                if (mTipoBusqueda == TipoBusqueda.Contribuciones && pFaceta.ClaveFaceta.Equals("gnoss:haspublicadorMostrarCom") && mEsMyGnoss)
                {
                    mostrarfaceta = false;
                }
                if (mTipoBusqueda == TipoBusqueda.Contribuciones && pFaceta.ClaveFaceta.Equals("gnoss:haspublicador") && !mEsMyGnoss)
                {
                    mostrarfaceta = false;
                }

                string consultaReciproca, claveFaceta = string.Empty;
                mFacetadoCL.FacetadoCN.FacetadoAD.ObtenerDatosFiltroReciproco(out consultaReciproca, pFaceta.ClaveFaceta, out claveFaceta);

                foreach (DataRow myrow in mFacetadoDS.Tables[claveFaceta].Rows)
                {
                    if (!myrow.IsNull(0) && !elementos.ContainsKey((string)myrow[0]))
                    {
                        string nombre = (string)myrow[0];
                        if (string.IsNullOrEmpty(nombre))
                        {
                            nombre = TextoSinEspecificar;
                        }
                        int cantidad = 0;
                        int.TryParse((string)myrow[1], out cantidad);
                        elementos.Add(nombre, cantidad);
                    }
                }

                if (mTipoBusqueda.Equals(TipoBusqueda.Mensajes) && claveFaceta.Equals("dce:type"))
                {
                    Dictionary<string, int> elmAux = new Dictionary<string, int>();

                    if (!elementos.ContainsKey("Entrada"))
                    {
                        elmAux.Add("Entrada", 0);
                    }
                    else
                    {
                        elmAux.Add("Entrada", elementos["Entrada"]);
                    }

                    if (!elementos.ContainsKey("Enviados"))
                    {
                        elmAux.Add("Enviados", 0);
                    }
                    else
                    {
                        elmAux.Add("Enviados", elementos["Enviados"]);
                    }

                    if (!elementos.ContainsKey("Eliminados"))
                    {
                        elmAux.Add("Eliminados", 0);
                    }
                    else
                    {
                        elmAux.Add("Eliminados", elementos["Eliminados"]);
                    }

                    elementos = elmAux;
                }


                if (mostrarfaceta)
                {
                    facetaModel = AgregarFaceta(claveFaceta, ObtenerNombreFaceta(pFaceta.Nombre), elementos, null, pLimite, pLimiteOriginal, pFaceta);
                }
            }

            if (facetaModel != null)
            {
                if (pPlegadas.HasValue)
                {
                    facetaModel.ShowWithoutItems = pPlegadas.Value;
                }
                if (pOneFacetRequest.HasValue)
                {
                    facetaModel.OneFacetRequest = pOneFacetRequest.Value;
                }

                //Se carga el Modelo de la faceta
                pListaFacetas.Add(facetaModel);
            }
        }

        private FacetModel CargarFacetaCategoriaArbol(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            List<Guid> ClaveCategorias = new List<Guid>();

            foreach (DataRow fila in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                Guid id = mUtilServiciosFacetas.ObtenerIDDesdeURI((string)fila[0]);

                if (GestorTesauro.ListaCategoriasTesauro.ContainsKey(id))
                {
                    bool ocultarcategoria = false;
                    if (mTipoBusqueda == TipoBusqueda.VerRecursosPerfil)
                    {
                        try
                        {
                            ocultarcategoria = GestorTesauro.TesauroDW.ListaTesauroUsuario.FirstOrDefault().CategoriaTesauroPublicoID != GestorTesauro.ListaCategoriasTesauro[id].PadreNivelRaiz.Clave;
                        }
                        catch (Exception)
                        {
                            ocultarcategoria = GestorTesauro.TesauroDW.ListaTesauroOrganizacion.FirstOrDefault().CategoriaTesauroPublicoID != GestorTesauro.ListaCategoriasTesauro[id].PadreNivelRaiz.Clave;
                        }
                    }

                    if (!ocultarcategoria)
                    {
                        if (!ClaveCategorias.Contains(id))
                        {
                            ClaveCategorias.Add(id);
                        }
                        string nombre = GestorTesauro.ListaCategoriasTesauro[id].Nombre[UtilIdiomas.LanguageCode];


                        if (!elementos.ContainsKey("gnoss:" + id.ToString().ToUpper()))
                        {
                            elementos.Add("gnoss:" + id.ToString().ToUpper(), PasarAEntero((string)fila[1]));
                            parametrosElementos.Add("gnoss:" + id.ToString().ToUpper(), nombre);
                        }
                    }
                }
            }
            return AgregarFacetaCatArbol("skos:ConceptID", UtilCadenas.ObtenerTextoDeIdioma(pFaceta.Nombre, UtilIdiomas.LanguageCode, ""), ClaveCategorias, elementos, parametrosElementos, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga la faceta de comunidades recomendadas
        /// </summary>
        /// <param name="pClaveFaceta">Clave de la faceta</param>
        /// <param name="pLimite">Límite de elementos</param>
        private FacetModel CargarFacetaComunidadesRecomendadas(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            List<Guid> ClaveIdentidades = new List<Guid>();

            foreach (DataRow fila in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                Guid id = mUtilServiciosFacetas.ObtenerIDDesdeURI((string)fila[0]);

                if (!ClaveIdentidades.Contains(id))
                {
                    ClaveIdentidades.Add(id);
                }
            }

            ProyectoCN proyectoCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            Dictionary<Guid, string> nombresIdentidades = proyectoCN.ObtenerNombreDeProyectosID(ClaveIdentidades);
            proyectoCN.Dispose();

            foreach (DataRow fila in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                Guid id = mUtilServiciosFacetas.ObtenerIDDesdeURI((string)fila[0]);

                string nombre = string.Empty;
                if (mTipoBusqueda.Equals(TipoBusqueda.EditarRecursosPerfil) && id.Equals(ProyectoAD.MetaProyecto))
                {
                    nombre = GetText("PERFIL", "MIESPACIOPERSONAL");
                }
                else if (mTipoBusqueda.Equals(TipoBusqueda.EditarRecursosPerfil) && id.Equals(ProyectoAD.MetaProyecto))
                {
                    nombre = GetText("PERFIL", "MISCONTRIBUCIONES");
                }
                else
                {
                    nombre = nombresIdentidades[id];
                }

                if (!elementos.ContainsKey(nombre))
                {
                    elementos.Add(nombre, PasarAEntero((string)fila[1]));
                    parametrosElementos.Add(nombre, "gnoss:" + id.ToString().ToUpper());
                }
            }

            string nombreFaceta = ObtenerNombreFaceta(pFaceta.Nombre);
            if (mTipoBusqueda.Equals(TipoBusqueda.EditarRecursosPerfil))
            {
                nombreFaceta = GetText("PERFILCONTRIBUCIONES", "PUBLICADOEN");
            }

            return AgregarFaceta(pFaceta.ClaveFaceta, nombreFaceta, elementos, parametrosElementos, pLimite, 0, pFaceta);
        }

        /// <summary>
        /// Carga la faceta de comunidades
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        private FacetModel CargarFacetaCom2(int pLimite, int pLimiteOriginal, Faceta pFaceta)
        {
            //Obtengo los proyectos del perfil
            IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
            List<Guid> ListaIdentidades = new List<Guid>();
            Dictionary<Guid, Guid> listaProyectosUsuario = new Dictionary<Guid, Guid>();

            if (!mIdentidadID.Equals(UsuarioAD.Invitado))
            {
                ListaIdentidades.Add(mIdentidadID);
                List<Guid> ListaPerfiles = identidadCN.ObtenerPerfilesDeIdentidades(ListaIdentidades);
                listaProyectosUsuario = identidadCN.ObtenerListaTodasMisIdentidadesDePerfil(true, ListaPerfiles[0]);
                identidadCN.Dispose();

                mFacetadoCL.ListaComunidadesPrivadasUsuario = mUtilServiciosFacetas.ObtenerListaComunidadesPrivadasUsuario(mIdentidadID, mEsUsuarioInvitado);
            }

            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            string nombre = "";
            foreach (DataRow fila in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                Guid id = mUtilServiciosFacetas.ObtenerIDDesdeURI((string)fila[0]);
                string parametro = "gnoss:" + id.ToString().ToUpper();

                if (id.Equals(ProyectoAD.MetaProyecto))
                {
                    nombre = GetText("CONFIGURACIONFACETADO", "ESPACIOSPERSONALES");
                    mListaPrivacidadProyecto.Add(parametro, true);
                }
                else if (!mListaPrivacidadProyecto.ContainsKey(parametro))
                {
                    ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);

                    Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.Proyecto filaProy = proyectoCL.ObtenerFilaProyecto(id);
                    nombre = filaProy.Nombre;
                    mListaPrivacidadProyecto.Add(parametro, filaProy.TipoAcceso.Equals((short)TipoAcceso.Publico) || filaProy.TipoAcceso.Equals((short)TipoAcceso.Restringido));
                }

                if (!elementos.ContainsKey(nombre))
                {

                    if (!(mTipoBusqueda == TipoBusqueda.Contribuciones))
                    {
                        elementos.Add(nombre, PasarAEntero((string)fila[1]));
                        parametrosElementos.Add(nombre, parametro);
                    }
                    else
                    {
                        if (listaProyectosUsuario.Keys.Contains((id)))
                        {
                            elementos.Add(nombre, PasarAEntero((string)fila[1]));
                            parametrosElementos.Add(nombre, parametro);
                        }
                    }
                }

            }

            return AgregarFaceta("sioc:has_space", GetText("COMMON", "COMUNIDADES"), elementos, parametrosElementos, pLimite, pLimiteOriginal, pFaceta);
        }

        /// <summary>
        /// Carga la faceta de comunidades de contactos
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        private FacetModel CargarFacetaComContactos(int pLimite, int pLimiteOriginal, Faceta pFaceta)
        {
            string listaProyectos = "";
            DataWrapperProyecto proyectosIdentidadDW = new DataWrapperProyecto();
            List<string> listaValores = new List<string>();

            foreach (DataRow fila in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                string idProyectoCompleto = (string)fila[0];
                string idProyecto = idProyectoCompleto.Substring("http://gnoss/".Length);
                listaValores.Add((string)fila[1]);
                idProyecto = $"'{idProyecto}";
                idProyecto += "',";
                listaProyectos += idProyecto;
            }

            listaProyectos = listaProyectos.Substring(0, listaProyectos.Length - 1);

            if (!mIdentidadID.Equals(UsuarioAD.Invitado))
            {
                ProyectoCN proyectoCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                proyectosIdentidadDW = proyectoCN.ObtenerProyectosParticipaPerfilIdentidad(listaProyectos, mIdentidadID);
            }

            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            string nombre = "";
            string parametro;
            string proyectoID;

            foreach (Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.Proyecto fila in proyectosIdentidadDW.ListaProyecto)
            {
                nombre = fila.Nombre;
                proyectoID = fila.ProyectoID.ToString().ToUpper();

                DataRow[] dr = mFacetadoDS.Tables[pFaceta.ClaveFaceta].Select("siochasspace2 = 'http://gnoss/" + proyectoID + "'");
                if (!elementos.ContainsKey(nombre))
                {
                    elementos.Add(nombre, int.Parse((dr[0][1]).ToString()));
                }
                else
                {
                    while (elementos.ContainsKey(nombre))
                    {
                        nombre += " ";
                    }
                    elementos.Add(nombre, int.Parse((dr[0][1]).ToString()));
                }
                parametro = $"gnoss:{proyectoID.ToString().ToUpper()}";
                parametrosElementos.Add(nombre, parametro);

            }

            return AgregarFaceta("sioc:has_space", GetText("COMMON", "COMUNIDADES"), elementos, parametrosElementos, pLimite, pLimiteOriginal, pFaceta);
        }

        /// <summary>
        /// Carga la faceta de tipo de contactos
        /// </summary>
        private FacetModel CargarFacetaTipoContactos(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            int numContactos = 0;
            bool esGrupo = false;
            bool esPersona = false;
            bool esOrg = false;

            if (mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows.Count > 0)
            {
                foreach (DataRow myrow in mFacetadoDS.Tables["rdf:type"].Rows)
                {
                    string tipo = ObtenerNombreTipoElemento((string)myrow[0]);

                    if (!elementos.ContainsKey(tipo))
                    {
                        int cantidad = 0;
                        int.TryParse((string)myrow[1], out cantidad);
                        elementos.Add(tipo, cantidad);
                        parametrosElementos.Add(tipo, (string)myrow[0]);
                    }

                    if (tipo.Contains("Personas") || tipo.Contains("Organizaciones"))
                    {
                        numContactos += int.Parse((string)myrow[1]);
                    }

                    if (tipo == "Personas")
                    {
                        esPersona = true;
                    }

                    if (tipo == "Organizaciones")
                    {
                        esOrg = true;
                    }

                    if (tipo == "Grupos")
                    {
                        esGrupo = true;
                    }
                }
                if (esPersona && esOrg && esGrupo)
                {
                    elementos.Add("Contactos", numContactos);
                }
                return AgregarFaceta("rdf:type", ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametrosElementos, pLimite, pFaceta);
            }

            return null;
        }

        /// <summary>
        /// Carga la faceta de estado de corrección
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        private FacetModel CargarFacetaEstadoCorreccion(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            foreach (DataRow myrow in mFacetadoDS.Tables["gnoss:hasEstadoCorreccion"].Rows)
            {
                if (!elementos.ContainsKey("gnoss:hasEstadoCorreccion"))
                {
                    int estado = 0;
                    int.TryParse((string)myrow[1], out estado);

                    if (((string)myrow[0]).Equals("1"))
                    {
                        elementos.Add(GetText("COMBUSQUEDAAVANZADA", "NOTIFICADONOCAMBIADO"), estado);
                        parametrosElementos.Add(GetText("COMBUSQUEDAAVANZADA", "NOTIFICADONOCAMBIADO"), (string)myrow[0]);
                    }

                    if (((string)myrow[0]).Equals("2"))
                    {
                        elementos.Add(GetText("COMBUSQUEDAAVANZADA", "NOTIFICADOCAMBIADO"), estado);
                        parametrosElementos.Add(GetText("COMBUSQUEDAAVANZADA", "NOTIFICADOCAMBIADO"), (string)myrow[0]);
                    }

                    if (((string)myrow[0]).Equals("3"))
                    {
                        elementos.Add(GetText("COMBUSQUEDAAVANZADA", "NOTIFICADONOCAMBIADO3DIAS"), estado);
                        parametrosElementos.Add(GetText("COMBUSQUEDAAVANZADA", "NOTIFICADONOCAMBIADO3DIAS"), (string)myrow[0]);
                    }
                }
            }
            return AgregarFaceta("gnoss:hasEstadoCorreccion", GetText("CONFIGURACIONFACETADO", "ESTADOCORRECCION"), elementos, parametrosElementos, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga la faceta de niveles de certificación
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        private FacetModel CargarFacetaNCer(int pLimite, string pNombreFaceta, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            foreach (DataRow myrow in mFacetadoDS.Tables["gnoss:hasnivelcertification"].Rows)
            {
                if (!elementos.ContainsKey("gnoss:hasnivelcertification"))
                {
                    string orden = (string)myrow[0];

                    int numeroOrden = 0;
                    int.TryParse((string)myrow[1], out numeroOrden);

                    //calculo Descripción a partir de orden
                    if (orden.Equals("100"))
                    {
                        elementos.Add(GetText("COMBUSQUEDAAVANZADA", "NOCERTIFI"), numeroOrden);

                        parametrosElementos.Add(GetText("COMBUSQUEDAAVANZADA", "NOCERTIFI"), orden);
                    }
                    else
                    {
                        ProyectoCN proyectoCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                        string descripcion = proyectoCN.ObtieneDescripciondeNivelCertificacion(orden, mProyectoID);
                        if ((descripcion != null) && (!elementos.ContainsKey(descripcion)))
                        {
                            parametrosElementos.Add(descripcion, orden);
                            elementos.Add(descripcion, numeroOrden);
                        }
                    }
                }
            }

            return AgregarFaceta("gnoss:hasnivelcertification", ObtenerNombreFaceta(pNombreFaceta), elementos, parametrosElementos, pLimite, pFaceta);
        }

        private FacetModel CargarFacetaEstadoUsuario(int pLimite, string pNombreFaceta, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            foreach (DataRow myrow in mFacetadoDS.Tables["gnoss:userstatus"].Rows)
            {
                if (!elementos.ContainsKey("gnoss:userstatus"))
                {
                    int estado = 0;
                    int.TryParse((string)myrow[1], out estado);

                    if (((string)myrow[0]).Equals("1"))
                    {
                        elementos.Add(GetText("ESTADOSUSUARIO", "ACTIVO"), estado);
                        parametrosElementos.Add(GetText("ESTADOSUSUARIO", "ACTIVO"), (string)myrow[0]);
                    }

                    if (((string)myrow[0]).Equals("2"))
                    {
                        elementos.Add(GetText("ESTADOSUSUARIO", "BLOQUEADO"), estado);
                        parametrosElementos.Add(GetText("ESTADOSUSUARIO", "BLOQUEADO"), (string)myrow[0]);
                    }

                    if (((string)myrow[0]).Equals("3"))
                    {
                        elementos.Add(GetText("ESTADOSUSUARIO", "EXPULSADO"), estado);
                        parametrosElementos.Add(GetText("ESTADOSUSUARIO", "EXPULSADO"), (string)myrow[0]);
                    }
                }
            }

            return AgregarFaceta("gnoss:userstatus", ObtenerNombreFaceta(pNombreFaceta), elementos, parametrosElementos, pLimite, pFaceta);
        }

        private FacetModel CargarFacetaRolUsuario(int pLimite, string pNombreFaceta, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            if (mFacetadoDS.Tables.Contains("gnoss:rol"))
            {
                foreach (DataRow myrow in mFacetadoDS.Tables["gnoss:rol"].Rows)
                {
                    int rol = 0;
                    int.TryParse((string)myrow[1], out rol);

                    if (((string)myrow[0]).Equals("0"))
                    {
                        elementos.Add(GetText("ROLESUSUARIO", "ADMINISTRADOR"), rol);
                        parametrosElementos.Add(GetText("ROLESUSUARIO", "ADMINISTRADOR"), (string)myrow[0]);
                    }

                    if (((string)myrow[0]).Equals("1"))
                    {
                        elementos.Add(GetText("ROLESUSUARIO", "SUPERVISOR"), rol);
                        parametrosElementos.Add(GetText("ROLESUSUARIO", "SUPERVISOR"), (string)myrow[0]);
                    }

                    if (((string)myrow[0]).Equals("2"))
                    {
                        elementos.Add(GetText("ROLESUSUARIO", "USUARIO"), rol);
                        parametrosElementos.Add(GetText("ROLESUSUARIO", "USUARIO"), (string)myrow[0]);
                    }
                }
            }
            if (elementos.Count > 0)
            {
                return AgregarFaceta("gnoss:rol", ObtenerNombreFaceta(pNombreFaceta), elementos, parametrosElementos, pLimite, pFaceta);
            }
            else
            {
                return null;
            }
        }

        private void CargarFacetaMultiple(int pLimite, Faceta pFaceta, List<FacetModel> pListaFacetas, bool? pPlegadas, bool? pOneFacetRequest)
        {
            //TODO: Migrar a EF
            //if (mFacetadoDS.Tables.Contains(pFaceta.ClaveFaceta))
            //{
            //    FacetaObjetoConocimientoProyecto filaFaceta = (FacetaObjetoConocimientoProyecto)pFaceta.FilaElementoEntity;

            //    if (filaFaceta.GetFacetaMultipleRows().Length > 0)
            //    {
            //        FacetaDS.FacetaMultipleRow filaFacetaMultiple = filaFaceta.GetFacetaMultipleRows()[0];
            //        foreach (DataRow myrow in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            //        {
            //            // Por cada fila, genero una faceta
            //            Dictionary<string, int> elementos = new Dictionary<string, int>();
            //            Dictionary<string, string> parametros = new Dictionary<string, string>();

            //            string facetaID = (string)myrow["facetID"];
            //            string nombreFaceta = UtilCadenas.ConvertirPrimeraLetraDeFraseAMayúsculas((string)myrow["facetName"]);

            //            if (mFacetadoDS.Tables.Contains($"{pFaceta.ClaveFaceta}_{facetaID}"))
            //            {
            //                foreach (DataRow filaItem in mFacetadoDS.Tables[$"{pFaceta.ClaveFaceta}_{facetaID}"].Rows)
            //                {
            //                    string nombre = (string)filaItem[0];
            //                    if (String.IsNullOrEmpty(nombre))
            //                    {
            //                        nombre = TextoSinEspecificar;
            //                    }
            //                    int cantidad = 0;
            //                    int.TryParse((string)filaItem[1], out cantidad);

            //                    elementos.Add(nombre, cantidad);
            //                    parametros.Add(nombre, $"{nombre}@@COND@@{filaFacetaMultiple.Filtro}={facetaID}");
            //                }
            //            }

            //            FacetModel facetaModel = AgregarFaceta(pFaceta.ClaveFaceta, ObtenerNombreFaceta(nombreFaceta), elementos, parametros, pLimite, pFaceta);
            //            facetaModel.Filter = facetaID;

            //            if (pPlegadas.HasValue)
            //            {
            //                facetaModel.ShowWithoutItems = pPlegadas.Value;
            //            }
            //            if (pOneFacetRequest.HasValue)
            //            {
            //                facetaModel.OneFacetRequest = pOneFacetRequest.Value;
            //            }

            //            pListaFacetas.Add(facetaModel);
            //        }
            //    }
            //}
        }

        [NonAction]
        public FacetModel AgregarFacetaCatArbol(string pClaveFaceta, string pTitulo, List<Guid> pListaCategorias, Dictionary<string, int> pElementosFaceta, Dictionary<string, string> pParametrosElementos, int pLimite, Faceta pFaceta)
        {
            List<Guid> categoriasFiltro = null;
            List<Guid> categoriasFiltroQuitar = new List<Guid>();
            List<CategoriaTesauro> listaCategorias = new List<CategoriaTesauro>();

            #region Cargamos categorías
            if (!mFacetasEnFormSem && (pFaceta.FilaElementoEntity is FacetaFiltroProyecto || pFaceta.FilaElementoEntity is FacetaFiltroHome))
            {
                //Esta faceta es para que se muestre sólo una categoría del tesauro. 
                try
                {
                    string filtro = "";
                    if (pFaceta.FilaElementoEntity is FacetaFiltroProyecto)
                    {
                        filtro = ((FacetaFiltroProyecto)(pFaceta.FilaElementoEntity)).Filtro;
                    }
                    if (pFaceta.FilaElementoEntity is FacetaFiltroHome)
                    {
                        filtro = ((FacetaFiltroHome)(pFaceta.FilaElementoEntity)).Filtro;
                    }

                    if (!string.IsNullOrEmpty(filtro))
                    {
                        categoriasFiltro = new List<Guid>();
                        string[] filtros = new string[1];
                        filtros[0] = filtro;

                        if (filtro.Contains("|"))
                        {
                            char[] separadores = { '|' };
                            filtros = filtro.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                        }

                        foreach (string filtroInt in filtros)
                        {
                            bool quitar = filtroInt.Contains("!");

                            Guid idCat = new Guid(filtroInt.Replace("!", ""));
                            if (GestorTesauro.ListaCategoriasTesauro.ContainsKey(idCat))
                            {
                                if (quitar)
                                {
                                    AgregarCategoriasHijasALista(GestorTesauro.ListaCategoriasTesauro[idCat], categoriasFiltroQuitar);
                                }
                                else
                                {
                                    CategoriaTesauro cat = GestorTesauro.ListaCategoriasTesauro[idCat];

                                    foreach (CategoriaTesauro hijo in cat.Hijos)
                                    {
                                        listaCategorias.Add(hijo);
                                    }

                                    AgregarCategoriasHijasALista(cat, categoriasFiltro);

                                    if (filtros.Length == 1)
                                    {
                                        pTitulo = cat.Nombre[UtilIdiomas.LanguageCode];
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception) { }
            }

            int limite = pLimite;
            if (pElementosFaceta.Keys.Count <= pLimite + 3)
            {
                limite = pLimite + 3;
            }
            int numElem = 0;
            List<string> filtrosFacetas = null;
            List<string> listaCategoriasExpandidas = new List<string>();

            if (mListaFiltros.ContainsKey(pClaveFaceta))
            {
                filtrosFacetas = mListaFiltros[pClaveFaceta];
                List<string> filtrosFacetasNombreReal = null;

                if ((mListaFiltrosFacetasNombreReal != null) && (mListaFiltrosFacetasNombreReal.ContainsKey(pClaveFaceta)))
                {
                    filtrosFacetasNombreReal = mListaFiltrosFacetasNombreReal[pClaveFaceta];
                }

                if ((filtrosFacetasNombreReal != null) && (filtrosFacetasNombreReal.Count > 0))
                {
                    //Se muestran primero los filtros seleccionados
                    foreach (string filtro in filtrosFacetas.ToArray())
                    {
                        TipoPropiedadFaceta tipoPropiedad = TipoPropiedadFaceta.NULL;
                        if (GestorFacetas.ListaFacetasPorClave.ContainsKey(filtro))
                        {
                            tipoPropiedad = GestorFacetas.ListaFacetasPorClave[filtro].TipoPropiedad;
                        }

                        string nombreReal = ObtenerNombreRealFiltro(filtrosFacetas, pClaveFaceta, filtro, tipoPropiedad);

                        CategoriaTesauro catFiltro = GestorTesauro.ListaCategoriasTesauro[new Guid(filtro.Replace("gnoss:", ""))];
                        bool perteneceFiltroAFaceta = categoriasFiltro == null || (categoriasFiltro.Count == 0 && categoriasFiltroQuitar.Count == 0) || categoriasFiltro.Contains(catFiltro.Clave) || (categoriasFiltroQuitar.Count > 0 && !categoriasFiltroQuitar.Contains(catFiltro.Clave));
                        IElementoGnoss catAux = catFiltro.Padre;
                        while (!(catAux is GestionTesauro))
                        {
                            listaCategoriasExpandidas.Add("gnoss:" + ((CategoriaTesauro)catAux).Clave.ToString().ToUpper());
                            catAux = catAux.Padre;
                        }

                        if (perteneceFiltroAFaceta)
                        {
                            if ((mListaFiltrosFacetasNombreReal != null))
                            {
                                filtrosFacetasNombreReal[numElem++] = nombreReal;
                            }
                        }
                    }
                }
            }

            if (categoriasFiltro == null || categoriasFiltro.Count == 0)
            {
                listaCategorias = new List<CategoriaTesauro>(GestorTesauro.ListaCategoriasTesauroPrimerNivel.Values);
            }
            #endregion

            FacetModel faceta = new FacetModel();
            faceta.Key = NormalizarNombreFaceta(pClaveFaceta);
            faceta.Name = pTitulo;
            faceta.OneFacetRequest = !string.IsNullOrEmpty(mFaceta);
            Guid filtroProyectoID;
            if (Guid.TryParse(pFaceta.FiltroProyectoID, out filtroProyectoID))
            {
                faceta.Filter = filtroProyectoID.ToString().ToUpper();
            }
            else
            {
                filtroProyectoID = ProyectoAD.MetaProyecto;
            }

            faceta.ThesaurusID = filtroProyectoID;

            faceta.FacetKey = pClaveFaceta;
            if (GruposPorTipo && pFaceta != null && pFaceta.ClaveFaceta != "rdf:type" && !FacetasComunesGrupos.Contains(pFaceta.ClaveFaceta) && !pClaveFaceta.StartsWith(pFaceta.ObjetoConocimiento + ";"))
            {
                if (mFaceta != null && mFaceta.Equals(pClaveFaceta) && mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1)
                {
                    faceta.FacetKey = $"{mListaFiltrosConGrupos["default;rdf:type"][0]};{pClaveFaceta}";
                }
                else
                {
                    faceta.FacetKey = $"{pFaceta.ObjetoConocimiento};{pClaveFaceta}";
                }
            }

            faceta.FacetItemList = CargarElementosHijosFaceta(listaCategorias, pListaCategorias, categoriasFiltroQuitar, pParametrosElementos, filtrosFacetas, pClaveFaceta, listaCategoriasExpandidas, pElementosFaceta, pFaceta.TipoPropiedad);

            CargarConfiguracionCajaBusquedaFaceta(faceta, pFaceta);
            faceta.Order = pFaceta.Orden;
            faceta.Multilanguage = pFaceta.MultiIdioma.ToString().ToLower();
            faceta.AutocompleteBehaviour = ObtenerBehaviourDeComportamiento(pFaceta.Comportamiento);

            return faceta;
        }

        [NonAction]
        private List<FacetItemModel> CargarElementosHijosFaceta(List<CategoriaTesauro> pListaCategorias, List<Guid> pListaCategoriasID, List<Guid> pListaCategoriasIDQuitar, Dictionary<string, string> pParametrosElementos, List<string> pFiltrosFacetas, string pClaveFaceta, List<string> pListaCategoriasExpandidas, Dictionary<string, int> pElementosFaceta, TipoPropiedadFaceta pTipoPropiedad)
        {
            List<FacetItemModel> listaItems = new List<FacetItemModel>();

            foreach (CategoriaTesauro catTes in pListaCategorias)
            {
                if (pListaCategoriasID.Contains(catTes.Clave) && !pListaCategoriasIDQuitar.Contains(catTes.Clave))
                {
                    string elemento = catTes.Nombre[UtilIdiomas.LanguageCode];
                    string parametro = elemento.Replace("'", "\'");

                    if ((pParametrosElementos != null) && pParametrosElementos[$"gnoss:{catTes.Clave.ToString().ToUpper()}"].Equals(elemento))
                    {
                        parametro = $"gnoss:{catTes.Clave.ToString().ToUpper()}";
                    }

                    bool pintarX = !((pFiltrosFacetas == null) || ((!pFiltrosFacetas.Contains(parametro)) && !pFiltrosFacetas.Contains(parametro)));
                    FacetItemModel facItemModel = AgregarElementoAFaceta(pClaveFaceta, elemento, parametro, pElementosFaceta[$"gnoss:{catTes.Clave.ToString().ToUpper()}"], pintarX, 0, true, 0, pListaCategoriasExpandidas, false, pParametrosElementos, TiposAlgoritmoTransformacion.Ninguno, pTipoPropiedad);

                    if (catTes.Hijos != null && catTes.Hijos.Count > 0)
                    {
                        List<CategoriaTesauro> listaCatsHijas = new List<CategoriaTesauro>();
                        foreach (CategoriaTesauro catHija in catTes.Hijos)
                        {
                            listaCatsHijas.Add(catHija);
                        }

                        facItemModel.FacetItemlist = CargarElementosHijosFaceta(listaCatsHijas, pListaCategoriasID, pListaCategoriasIDQuitar, pParametrosElementos, pFiltrosFacetas, pClaveFaceta, pListaCategoriasExpandidas, pElementosFaceta, pTipoPropiedad);
                    }

                    listaItems.Add(facItemModel);
                }
            }

            return listaItems;
        }

        [NonAction]
        private FacetModel CargarFacetaRangosFechaDinamica(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametros = new Dictionary<string, string>();
            int fechaAnterior = 0;
            int count = 0;
            foreach (DataRow myrow3 in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                bool esRangoAnteriorA = false;
                int cantidad = int.Parse(myrow3[1].ToString());
                if (cantidad > 0)
                {
                    string fechaString = myrow3[0].ToString();
                    if (fechaString.StartsWith("-"))
                    {
                        esRangoAnteriorA = true;
                        fechaString = fechaString.Substring(1);
                    }
                    int fecha = 0;
                    if (!int.TryParse(fechaString, out fecha) && fechaString.Length >= 8)
                    {
                        fechaString = fechaString.Substring(0, 8);
                        fecha = int.Parse(fechaString);
                    }

                    string suplementoCeros = "";

                    while (fechaString.Length + suplementoCeros.Length < 8)
                    {
                        //hasta que los rangos no tengan 14 caracteres tengo que ponerle ceros (8 cifras para el año y 6 para la hora)
                        suplementoCeros += "0";
                    }

                    int suma = 1;
                    if (fechaString.Length == 6)
                    {
                        // estamos haciendo rangos por meses
                        fechaString = (fecha - 1).ToString();
                    }
                    else if (fechaString.Length == 4)
                    {
                        if (count < mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows.Count - 1)
                        {
                            fechaString = (fecha + 1).ToString();
                        }
                        else if (fechaAnterior != 0)
                        {
                            fecha = fechaAnterior + 1;
                            fechaString = (fechaAnterior + 1).ToString();
                        }
                    }

                    string filtro = "";
                    if (!esRangoAnteriorA)
                    {
                        filtro = fechaString + suplementoCeros;
                    }
                    else
                    {
                        suma = 0;
                    }

                    if (fechaAnterior != 0 && count < mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows.Count - 1)
                    {
                        suma = fechaAnterior - fecha + 1;
                    }

                    if (suplementoCeros.Equals("00"))
                    {
                        int agno = fecha / 100;
                        int mes = (fecha % 100) + suma;
                        //Si es un filtro por mes, sumamos uno más a la suma del mes.
                        suplementoCeros = DateTime.DaysInMonth(agno, mes).ToString();
                    }

                    if (count < mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows.Count - 1)
                    {
                        int filtroMay = 0;
                        int filtroMenor = 0;
                        if (int.TryParse(filtro, out filtroMenor) && int.TryParse((fecha + suma).ToString() + suplementoCeros, out filtroMay))
                        {
                            if (filtroMay == filtroMenor)
                            {
                                filtro = $"{(fecha + suma - 1).ToString()}{suplementoCeros}-{filtroMay}";
                            }
                            else if (filtroMay > filtroMenor)
                            {
                                //Los filtros tienen que ir de menor a mayor
                                filtro = $"{filtroMenor}-{filtroMay}";
                            }
                            else
                            {
                                filtro = $"{filtroMay}-{filtroMenor}";
                            }
                        }
                        else if (string.IsNullOrEmpty(filtro))
                        {
                            //Los años vienen con el filtro null para el primer elemento de la carga.
                            filtro = $"-{(fecha + suma)}{suplementoCeros}";
                        }
                    }
                    else
                    {
                        filtro += "-";
                    }

                    List<string> listaFiltros = new List<string>();
                    if (mListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                    {
                        listaFiltros = mListaFiltros[pFaceta.ClaveFaceta];
                    }

                    string nombre = ObtenerNombreRealFiltro(listaFiltros, pFaceta.ClaveFaceta, filtro, pFaceta.TipoPropiedad);
                    if (!parametros.ContainsKey(nombre))
                    {

                        parametros.Add(nombre, filtro);
                    }
                    if (!elementos.ContainsKey(nombre))
                    {
                        elementos.Add(nombre, cantidad);
                    }

                    if (fecha.ToString().Length == 4)
                    {
                        //Solo para Agnos
                        fechaAnterior = fecha;
                    }
                }
                count++;
            }
            return AgregarFaceta(pFaceta.ClaveFaceta, ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametros, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga una faceta de rangos de fecha
        /// </summary>
        /// <param name="pClaveFaceta">Clave de la faceta</param>
        /// <param name="pNombreFaceta">Nombre de la faceta</param>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaRangosSiglosDinamica(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametros = new Dictionary<string, string>();
            int sigloAnterior = 0;
            int count = 0;
            foreach (DataRow myrow3 in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                int cantidad = int.Parse(myrow3[1].ToString());
                if (cantidad > 0)
                {
                    string sigloString = myrow3[0].ToString();
                    int filtroMayor = 0;
                    if (!int.TryParse(sigloString, out filtroMayor) && sigloString.Length >= 8)
                    {
                        sigloString = sigloString.Substring(0, 8);
                        filtroMayor = int.Parse(sigloString);
                    }

                    string suplementoCeros = "";
                    int sumaFiltroMayor = 1;
                    //8 ceros corresponde a un milenio, ejemplo: 1900, 1800...
                    int numCerosBucle = 8;
                    if (sigloString.Length == 1)
                    {
                        //7 ceros corresponde a una centura, ejemplo: 900, 700...
                        numCerosBucle = 7;
                    }

                    //Cuando hay un filtro se están trayendo decenios
                    if (sigloString.Length == 4 && mListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                    {
                        sumaFiltroMayor = 10;
                    }
                    else if (sigloString.Length == 3 && mListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                    {
                        //Si hay un filtro aplicado y la longitud es de 2, se ponen 6 ceros porque corresponde a un centura, ejemplo: 100, 200, 300...
                        numCerosBucle = 7;
                        sumaFiltroMayor = 10;
                    }
                    else if (sigloString.Length == 1 && sigloString.Equals("0") && mListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                    {
                        //Si hay un filtro aplicado y la longitud es de 1, se ponen 6 ceros porque corresponde a un año, ejemplo: 1, 2, 3...
                        numCerosBucle = 5;
                        sumaFiltroMayor = 10;
                    }
                    else if (sigloString.Length == 2 && mListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                    {
                        //Si hay un filtro aplicado y la longitud es de 1, se ponen 6 ceros porque corresponde a un año, ejemplo: 1, 2, 3...
                        numCerosBucle = 6;
                        sumaFiltroMayor = 10;
                    }

                    while (sigloString.Length + suplementoCeros.Length < numCerosBucle)
                    {
                        //hasta que los rangos no tengan 8 caracteres tengo que ponerle ceros 
                        suplementoCeros += "0";
                    }

                    string filtro = "";
                    filtro = $"{filtroMayor}{suplementoCeros}-{(filtroMayor + sumaFiltroMayor)}{suplementoCeros}";

                    List<string> listaFiltros = new List<string>();
                    if (mListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                    {
                        listaFiltros = mListaFiltros[pFaceta.ClaveFaceta];
                    }

                    string nombre = ObtenerNombreRealFiltro(listaFiltros, pFaceta.ClaveFaceta, filtro, pFaceta.TipoPropiedad);

                    if (!parametros.ContainsKey(nombre))
                    {

                        parametros.Add(nombre, filtro);
                    }
                    if (!elementos.ContainsKey(nombre))
                    {
                        elementos.Add(nombre, cantidad);
                    }

                    sigloAnterior = filtroMayor;
                }
                count++;
            }
            return AgregarFaceta(pFaceta.ClaveFaceta, ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametros, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga una faceta con la fecha mínima y máxima
        /// </summary>
        /// <param name="pClaveFaceta">Clave de la faceta</param>
        /// <param name="pNombreFaceta">Nombre de la faceta</param>
        /// <param name="pLimite">Límite de elementos</param>
        private FacetModel CargarFacetaFechaMinMax(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametros = new Dictionary<string, string>();

            if (mFacetadoDS.Tables.Contains(pFaceta.ClaveFaceta))
            {
                if (mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows != null && mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows.Count > 0)
                {
                    elementos.Add("fechaMin", int.Parse(mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows[0].ItemArray[0].ToString()));
                    elementos.Add("fechaMax", int.Parse(mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows[0].ItemArray[1].ToString()));
                }
            }

            return AgregarFaceta(pFaceta.ClaveFaceta, ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametros, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga una faceta de rangos de fecha
        /// </summary>
        /// <param name="pClaveFaceta">Clave de la faceta</param>
        /// <param name="pNombreFaceta">Nombre de la faceta</param>
        /// <param name="pLimite">Límite de elementos</param>

        /// <summary>
        /// Carga la faceta de extensión de documento
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaTipoDocExt(int limite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            foreach (DataRow myrow in mFacetadoDS.Tables["gnoss:hastipodocExt"].Rows)
            {
                if (!elementos.ContainsKey("gnoss:hastipodocExt"))
                {
                    int tipoDoc = 0;
                    string s = (string)myrow[0];
                    bool result = int.TryParse(s, out tipoDoc);
                    string tipo = "";

                    if (result)
                    {
                        tipo = ObtenerNombreTipoDocumento(tipoDoc);
                    }
                    else
                    {
                        if (s.Contains("@"))
                        {
                            tipo = ObtenerNombreFaceta(s);
                        }
                        else
                        {
                            tipo = s;
                        }
                    }
                    tipo = tipo.ToLower();

                    elementos.Add(tipo, int.Parse((string)myrow[1]));
                    parametrosElementos.Add(tipo, s);
                }
            }
            return AgregarFaceta("gnoss:hastipodocExt", ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametrosElementos, limite, pFaceta);
        }

        [NonAction]
        private FacetModel CargarFacetaTipoDoc(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            foreach (DataRow myrow in mFacetadoDS.Tables["gnoss:hastipodoc"].Rows)
            {
                if (!elementos.ContainsKey("gnoss:hastipodoc"))
                {
                    int tipoDoc = 0;
                    int cantidad = 0;

                    int.TryParse((string)myrow[0], out tipoDoc);
                    int.TryParse((string)myrow[1], out cantidad);
                    string tipo = ObtenerNombreTipoDocumento(tipoDoc);
                    elementos.Add(tipo, cantidad);
                    parametrosElementos.Add(tipo, (string)myrow[0]);
                }
            }
            return AgregarFaceta("gnoss:hastipodoc", ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametrosElementos, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga la faceta Explora...
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaTipoElemento(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            if (mNecesarioMostarTiposElementos || (mFacetasHomeCatalogo && mFacetadoDS.Tables["rdf:type"].Rows.Count > 1))
            {
                foreach (DataRow myrow in mFacetadoDS.Tables["rdf:type"].Rows)
                {
                    if (!elementos.ContainsKey("rdf:type"))
                    {
                        string tipo = ObtenerNombreTipoElemento((string)myrow[0]);
                        if (!(mTipoBusqueda == TipoBusqueda.Contribuciones && tipo.Equals("RecursoPerfilPersonal")))
                        {
                            if (tipo.Contains("@"))
                            {
                                tipo = ObtenerNombreFaceta(tipo);
                            }
                            if (!elementos.ContainsKey(tipo))
                            {
                                int cantidad = 0;
                                int.TryParse((string)myrow[1], out cantidad);
                                elementos.Add(tipo, cantidad);
                                parametrosElementos.Add(tipo, (string)myrow[0]);
                            }
                        }
                    }
                }

                return AgregarFaceta("rdf:type", ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametrosElementos, pLimite, pFaceta);
            }
            return null;
        }

        /// <summary>
        /// Carga la faceta de estado
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaEstado(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();

            if (mFacetadoDS.Tables.Contains("gnoss:hasestado"))
            {
                foreach (DataRow myrow in mFacetadoDS.Tables["gnoss:hasestado"].Rows)
                {
                    if (!elementos.ContainsKey("gnoss:hasestado"))
                    {
                        int estado = 0;
                        int.TryParse((string)myrow[1], out estado);

                        if (((string)myrow[0]).Equals("0"))
                        {
                            elementos.Add(GetText("COMBUSQUEDAAVANZADA", "EDICION"), estado);
                        }
                        else if (((string)myrow[0]).Equals("1"))
                        {
                            elementos.Add(GetText("COMBUSQUEDAAVANZADA", "VOTACION"), estado);
                        }
                        else if (((string)myrow[0]).Equals("2"))
                        {
                            elementos.Add(GetText("COMBUSQUEDAAVANZADA", "UNIFICACION"), estado);
                        }
                        else if (((string)myrow[0]).Equals("3"))
                        {
                            elementos.Add(GetText("COMBUSQUEDAAVANZADA", "FINALIZADO"), estado);
                        }
                        else
                        {
                            //TODO: Alberto, poner los textos dependientes del fichero de idiomas para que no se traigan de virtuoso con fallos/errores.
                            elementos.Add((string)myrow[0], estado);
                        }
                    }
                }
            }
            if (elementos.Count > 0)
            {
                return AgregarFaceta("gnoss:hasEstado", ObtenerNombreFaceta(pFaceta.Nombre), elementos, null, pLimite, pFaceta);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Carga una faceta de rangos númericos
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaRangos(int pLimite, Faceta pFaceta)
        {
            //Hayamos los cuartiles 
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            if (pFaceta.Comportamiento != TipoMostrarSoloCaja.SoloCajaSiempre)
            {
                foreach (DataRow myrow3 in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
                {
                    int cantidad = PasarAEntero((string)myrow3[1]);
                    string valor = (string)myrow3[0];

                    string texto = "";
                    if (valor.Contains("-"))
                    {
                        string[] delimiter = { "-" };
                        string[] valores = valor.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
                        if (valores.Length > 1)
                        {
                            texto = $"{GetText("COMBUSQUEDAAVANZADA", "DE")} {valores[0]} {GetText("COMBUSQUEDAAVANZADA", "A")} {valores[1]}";
                        }
                        else
                        {
                            texto = valores[0];
                        }
                    }
                    else
                    {
                        //Nunca debería entrar por aquí...
                        texto = valor;
                    }

                    if (!elementos.ContainsKey(texto))
                    {
                        elementos.Add(texto, cantidad);
                        parametrosElementos.Add(texto, valor);
                    }
                }
            }

            return AgregarFaceta(pFaceta.ClaveFaceta, ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametrosElementos, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga la faceta de código postal
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaCodPost(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();

            foreach (DataRow myrow3 in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                string cp = (string)myrow3[0];

                if (cp.Length - 3 > 0)
                {
                    cp = cp.Substring(0, cp.Length - 3);
                }
                else { cp = "00"; }

                cp = $"{cp}###";

                if (elementos.ContainsKey(cp))
                {
                    elementos[cp] = elementos[cp] + 1;
                }
                else
                {
                    elementos.Add(cp, 1);
                }
            }
            return AgregarFaceta(pFaceta.ClaveFaceta, ObtenerNombreFaceta(pFaceta.Nombre), elementos, null, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga una faceta de fecha sin rangos
        /// </summary>
        /// <param name="pClaveFaceta">Clave de la faceta</param>
        /// <param name="pNombreFaceta">Nombre de la faceta</param>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaFechaDinamica(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametros = new Dictionary<string, string>();

            if (mFacetadoDS.Tables.Contains(pFaceta.ClaveFaceta))
            {
                foreach (DataRow myrow3 in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
                {
                    int cantidad = int.Parse(myrow3[1].ToString());
                    if (cantidad > 0)
                    {
                        string fechaString = myrow3[0].ToString();
                        if (fechaString.StartsWith("-"))
                        {
                            fechaString = fechaString.Substring(1);
                        }
                        int fecha = 0;
                        if (!int.TryParse(fechaString, out fecha) && fechaString.Length >= 8)
                        {
                            fechaString = fechaString.Substring(0, 8);
                            fecha = int.Parse(fechaString);
                        }

                        string suplementoCeros = "";

                        while (fechaString.Length + suplementoCeros.Length < 8)
                        {
                            //hasta que los rangos no tengan 14 caracteres tengo que ponerle ceros (8 cifras para el año y 6 para la hora)
                            suplementoCeros += "0";
                        }

                        fechaString = fecha.ToString();

                        string filtro = fechaString + suplementoCeros;

                        if (suplementoCeros.Equals("00"))
                        {
                            int agno = fecha / 100;
                            //Rangos
                            int mes = (fecha % 100);
                            //Si es un filtro por mes, sumamos uno más a la suma del mes.
                            suplementoCeros = DateTime.DaysInMonth(agno, mes).ToString();
                        }

                        int filtroMay = 0;
                        int filtroMenor = 0;
                        //RangoMeses
                        if (int.TryParse(filtro, out filtroMenor) && int.TryParse((fecha).ToString() + suplementoCeros, out filtroMay))
                        {
                            if (fechaString.Length == 4)
                            {
                                int.TryParse((fecha + 1).ToString() + suplementoCeros, out filtroMay);
                            }

                            if (filtroMay == filtroMenor)
                            {
                                filtro = $"{fecha.ToString()}{suplementoCeros}-{filtroMay}";
                            }
                            else if (filtroMay > filtroMenor)
                            {
                                filtro = $"{filtroMenor}-{filtroMay}";
                            }
                            else
                            {
                                filtro = $"{filtroMay}-{filtroMenor}";
                            }
                        }
                        else if (string.IsNullOrEmpty(filtro))
                        {
                            //Los años vienen con el filtro null para el primer elemento de la carga.
                            filtro = $"-{fecha}{suplementoCeros}";
                        }

                        List<string> listaFiltros = new List<string>();
                        if (mListaFiltros.ContainsKey(pFaceta.ClaveFaceta))
                        {
                            listaFiltros = mListaFiltros[pFaceta.ClaveFaceta];
                        }

                        string nombre = ObtenerNombreRealFiltro(listaFiltros, pFaceta.ClaveFaceta, filtro, pFaceta.TipoPropiedad);
                        if (!parametros.ContainsKey(nombre))
                        {

                            parametros.Add(nombre, filtro);
                        }
                        if (!elementos.ContainsKey(nombre))
                        {
                            elementos.Add(nombre, cantidad);
                        }
                    }
                }

            }
            return AgregarFaceta(pFaceta.ClaveFaceta, ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametros, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga la faceta de categorías del tesauro
        /// </summary>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaCategoria(int pLimite, Faceta pFaceta)
        {
            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            List<Guid> categoriasFiltroQuitar = new List<Guid>();
            if (pFaceta.FilaElementoEntity is FacetaFiltroProyecto || pFaceta.FilaElementoEntity is FacetaFiltroHome)
            {
                //Esta faceta es para que se muestre sólo una categoría del tesauro. 
                try
                {
                    string filtro = "";

                    if (pFaceta.FilaElementoEntity is FacetaFiltroProyecto)
                    {
                        filtro = ((FacetaFiltroProyecto)(pFaceta.FilaElementoEntity)).Filtro;
                    }
                    if (pFaceta.FilaElementoEntity is FacetaFiltroHome)
                    {
                        filtro = ((FacetaFiltroHome)(pFaceta.FilaElementoEntity)).Filtro;
                    }
                    string[] filtros = new string[1];
                    filtros[0] = filtro;

                    if (filtro.Contains("|"))
                    {
                        char[] separadores = { '|' };
                        filtros = filtro.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
                    }

                    foreach (string filtroInt in filtros)
                    {
                        bool quitar = filtroInt.Contains("!");

                        Guid idCat = new Guid(filtroInt.Replace("!", ""));
                        if (GestorTesauro.ListaCategoriasTesauro.ContainsKey(idCat))
                        {
                            if (quitar)
                            {
                                AgregarCategoriasHijasALista(GestorTesauro.ListaCategoriasTesauro[idCat], categoriasFiltroQuitar);
                            }
                        }
                    }
                }
                catch (Exception) { }
            }

            foreach (DataRow fila in mFacetadoDS.Tables[pFaceta.ClaveFaceta].Rows)
            {
                Guid id = mUtilServiciosFacetas.ObtenerIDDesdeURI((string)fila[0]);

                if (GestorTesauro.ListaCategoriasTesauro.ContainsKey(id) && !categoriasFiltroQuitar.Contains(id))
                {
                    string nombre = GestorTesauro.ListaCategoriasTesauro[id].Nombre[UtilIdiomas.LanguageCode];

                    if (!elementos.ContainsKey(nombre))
                    {
                        elementos.Add(nombre, PasarAEntero((string)fila[1]));
                        parametrosElementos.Add(nombre, $"gnoss:{id.ToString().ToUpper()}");
                    }
                }
            }
            return AgregarFaceta("skos:ConceptID", UtilCadenas.ObtenerTextoDeIdioma(pFaceta.Nombre, UtilIdiomas.LanguageCode, ""), elementos, parametrosElementos, pLimite, pFaceta);
        }

        /// <summary>
        /// Carga las categorías de la home de un catálogo.
        /// </summary>
        /// <param name="pModoArbol">Verdad si se deben cargar en modo árbol</param>
        [NonAction]
        private FacetModel CargarFacetaCategoriaHomeCatalogo(int pLimite, bool pModoArbol, Faceta pFaceta)
        {
            string urllinkCategoria = mControladorBase.UrlsSemanticas.GetURLBaseRecursos(mUtilServicios.UrlIntragnoss, UtilIdiomas, FilaProyecto.NombreCorto, "/", false);

            Dictionary<string, int> elementos = new Dictionary<string, int>();
            Dictionary<string, string> parametrosElementos = new Dictionary<string, string>();

            foreach (CategoriaTesauro catTesauro in GestorTesauro.ListaCategoriasTesauroPrimerNivel.Values)
            {
                string urlFinal = "";
                string nombre = "";
                string imagen = "";

                if (pFaceta != null)
                {
                    urllinkCategoria = mControladorBase.UrlsSemanticas.ObtenerURLComunidad(UtilIdiomas, mUtilServicios.UrlIntragnoss, FilaProyecto.NombreCorto);

                    string pagina = pFaceta.PestanyaFaceta;

                    if (pagina == "busqueda")
                    {
                        pagina = GetText("URLSEM", "BUSQUEDAAVANZADA");
                    }
                    else if (pagina == "recursos")
                    {
                        pagina = GetText("URLSEM", "RECURSOS");
                    }
                    else if (pagina == "debates")
                    {
                        pagina = GetText("URLSEM", "DEBATES");
                    }
                    else if (pagina == "preguntas")
                    {
                        pagina = GetText("URLSEM", "PREGUNTAS");
                    }
                    else if (pagina == "encuestas")
                    {
                        pagina = GetText("URLSEM", "ENCUESTAS");
                    }
                    else if (pagina == "personas-y-organizaciones")
                    {
                        pagina = GetText("URLSEM", "PERSONASYORGANIZACIONES");
                    }

                    urllinkCategoria += $"/{pagina}";
                    urlFinal = $"{urllinkCategoria}#skos:ConceptID=gnoss:{catTesauro.Clave.ToString().ToUpper()}";
                }
                else
                {
                    urlFinal = $"{urllinkCategoria}#skos:ConceptID=gnoss:{catTesauro.Clave.ToString().ToUpper()}";
                }

                nombre = catTesauro.Nombre[UtilIdiomas.LanguageCode];

                if (catTesauro.FilaCategoria.TieneFoto)
                {
                    imagen = mUtilServicios.UrlIntragnoss + UtilArchivos.ContentImagenes + catTesauro.UrlImagenMini;
                }
                string numRecTexto = catTesauro.NombreConNumeroRecursos[UtilIdiomas.LanguageCode];

                elementos.Add(nombre, PasarAEntero(numRecTexto));
                parametrosElementos.Add(nombre, urlFinal);
            }

            return AgregarFaceta(pFaceta.ClaveFaceta, ObtenerNombreFaceta(pFaceta.Nombre), elementos, parametrosElementos, pLimite, pFaceta);
        }


        /// <summary>
        /// Carga la faceta de categorías semánticas teniendo en cuenta si hay que pintar un rango o los elementos de un nivel.
        /// </summary>
        /// <param name="pClaveFaceta">Clave de la faceta</param>
        /// <param name="pLimite">Límite de elementos</param>
        [NonAction]
        private FacetModel CargarFacetaTesauroSemantico(int pLimite, Faceta pFaceta, bool pOrdenarPorNumero)
        {
            string nivelSemantico = null;
            FacetadoDS facetadoDatos = null;

            if ((pFaceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemantico || pFaceta.AlgoritmoTransformacion == TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado) && (pFaceta.FilaElementoEntity is FacetaFiltroProyecto || pFaceta.FilaElementoEntity is FacetaFiltroHome) && !string.IsNullOrEmpty(pFaceta.FiltroProyectoID) && pFaceta.FiltroProyectoID.Split(';')[0].Contains("-"))
            {
                if (!mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
                {
                    mFacetadoDSAuxPorFaceta.Add(pFaceta.ClaveFaceta, new List<KeyValuePair<string, FacetadoDS>>());
                }
                mFacetadoDSAuxPorFaceta[pFaceta.ClaveFaceta].Add(new KeyValuePair<string, FacetadoDS>(pFaceta.FiltroProyectoID, mFacetadoDS));
            }

            if (mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
            {
                foreach (KeyValuePair<string, FacetadoDS> pair in mFacetadoDSAuxPorFaceta[pFaceta.ClaveFaceta])
                {
                    if (pair.Key == pFaceta.FiltroProyectoID)
                    {
                        facetadoDatos = pair.Value;
                        break;
                    }
                }
                nivelSemantico = pFaceta.FiltroProyectoID.Split(';')[0];
            }
            else
            {
                facetadoDatos = mFacetadoDS;
            }

            if (!string.IsNullOrEmpty(nivelSemantico))
            {
                if (nivelSemantico.Contains("-"))
                {
                    //Si es un rango separado por 1-3 pintamos en formato árbol
                    return CargarFacetaTesauroSemanticoArbol(pLimite, pFaceta, facetadoDatos, pOrdenarPorNumero);
                }
                else
                {
                    //Si es un número fijo pintamos ese nivel.
                    return CargarFacetaTesauroSemanticoNormal(pLimite, pFaceta, facetadoDatos, pOrdenarPorNumero);
                }
            }
            else
            {
                return CargarFacetaTesauroSemanticoNormal(pLimite, pFaceta, facetadoDatos, pOrdenarPorNumero);
            }
        }

        [NonAction]
        private FacetModel CargarFacetaTesauroSemanticoNormal(int pLimite, Faceta pFaceta, FacetadoDS pFacetaDatos, bool pOrdenarPorNumero)
        {
            string[] arrayTesSem = ObtenerDatosFacetaTesSem(pFaceta.ClaveFaceta);

            string tituloFac = ObtenerNombreFaceta(pFaceta.Nombre);

            if (mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
            {
                tituloFac = UtilCadenas.ObtenerTextoDeIdioma(pFaceta.FiltroProyectoID.Split(';')[2], mLanguageCode, ParametrosGenerales.IdiomaDefecto);
            }

            Dictionary<string, int> elementos = new Dictionary<string, int>();
            foreach (DataRow myrow in pFacetaDatos.Tables[pFaceta.ClaveFaceta].Rows)
            {
                if (!elementos.ContainsKey((string)myrow[0]))
                {
                    string nombre = (string)myrow[0];
                    if (String.IsNullOrEmpty(nombre))
                    {
                        nombre = TextoSinEspecificar;
                    }
                    int cantidad = 0;
                    int.TryParse((string)myrow[1], out cantidad);


                    elementos.Add(nombre, cantidad);
                }
            }

            #region Cargo props Tesauro Semántico

            FacetadoDS facetadoTesSemDS = null;

            if (!TesauroSemDSFaceta.ContainsKey(pFaceta.ClaveFaceta) || mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
            {
                List<string> listaPropsTesSem = new List<string>();
                listaPropsTesSem.Add(arrayTesSem[2]);
                listaPropsTesSem.Add(arrayTesSem[3]);

                List<string> listaEntidadesBusqueda = new List<string>();

                if (mListaFiltros.ContainsKey(pFaceta.ClaveFaceta) && !mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
                {
                    listaEntidadesBusqueda.AddRange(mListaFiltros[pFaceta.ClaveFaceta]);
                }

                listaEntidadesBusqueda.AddRange(elementos.Keys);

                FacetadoCN facCN = new FacetadoCN(mUtilServicios.UrlIntragnoss, mEntityContext, mLoggingService, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                facetadoTesSemDS = facCN.ObtenerValoresPropiedadesEntidades(mGrafoID, listaEntidadesBusqueda, listaPropsTesSem, true);
                facCN.Dispose();

                if (!TesauroSemDSFaceta.ContainsKey(pFaceta.ClaveFaceta))
                {
                    TesauroSemDSFaceta.Add(pFaceta.ClaveFaceta, facetadoTesSemDS);
                }
            }
            else
            {
                facetadoTesSemDS = TesauroSemDSFaceta[pFaceta.ClaveFaceta];
            }

            #endregion

            string idPanel = NormalizarNombreFaceta(pFaceta.ClaveFaceta);
            string claveFacetaANSI = pFaceta.ClaveFaceta;
            string Titulo = tituloFac;

            //Obtengo la url a la que hay que redireccionar en caso de que estemos en la home de una comunidad
            string url = "";
            string accion = "";
            if (mFacetasHomeCatalogo)
            {
                url = ObtenerUrlPaginaActual(pFaceta);
                accion = $"document.location = '{url}?";
            }

            int limite = pLimite;

            if (elementos.Keys.Count <= pLimite + 3)
            {
                limite = pLimite + 3;
            }
            int numElem = 0;

            FacetModel facetaModel = new FacetModel();
            facetaModel.FacetItemList = new List<FacetItemModel>();
            facetaModel.ThesaurusID = Guid.Empty;
            facetaModel.Key = idPanel;
            facetaModel.Name = Titulo;
            facetaModel.OneFacetRequest = !string.IsNullOrEmpty(mFaceta);

            facetaModel.FacetKey = pFaceta.ClaveFaceta;
            if (GruposPorTipo && pFaceta != null && pFaceta.ClaveFaceta != "rdf:type" && !FacetasComunesGrupos.Contains(pFaceta.ClaveFaceta) && !pFaceta.ClaveFaceta.StartsWith(pFaceta.ObjetoConocimiento + ";"))
            {
                if (mFaceta != null && mFaceta.Equals(pFaceta.ClaveFaceta) && mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1)
                {
                    facetaModel.FacetKey = $"{mListaFiltrosConGrupos["default;rdf:type"][0]};{pFaceta.ClaveFaceta}";
                }
                else
                {
                    facetaModel.FacetKey = $"{pFaceta.ObjetoConocimiento};{pFaceta.ClaveFaceta}";
                }
            }

            CargarConfiguracionCajaBusquedaFaceta(facetaModel, pFaceta);
            facetaModel.Order = pFaceta.Orden;
            facetaModel.Multilanguage = pFaceta.MultiIdioma.ToString().ToLower();
            facetaModel.AutocompleteBehaviour = ObtenerBehaviourDeComportamiento(pFaceta.Comportamiento);

            List<string> filtrosFacetas = null;
            List<string> filtrosUsuarios = null;

            if (mListaFiltros.ContainsKey(pFaceta.ClaveFaceta) && !mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
            {
                filtrosFacetas = mListaFiltros[pFaceta.ClaveFaceta];
                filtrosUsuarios = null;
                if (mListaFiltrosFacetasUsuario.ContainsKey(pFaceta.ClaveFaceta))
                {
                    filtrosUsuarios = this.mListaFiltrosFacetasUsuario[pFaceta.ClaveFaceta];
                }

                //Se muestran primero los filtros seleccionados por el usuario con una X
                foreach (string filtro in filtrosFacetas.ToArray())
                {
                    string nombreReal = ObtenerPropTesSem(facetadoTesSemDS, arrayTesSem[3], filtro);

                    if (mListaFiltrosFacetasUsuario.ContainsKey(pFaceta.ClaveFaceta) && mListaFiltrosFacetasUsuario[pFaceta.ClaveFaceta].Contains(filtro))
                    {
                        int numElementos = -1;

                        FacetItemModel facetaItemModel = AgregarElementoAFaceta(pFaceta.ClaveFaceta, nombreReal, filtro, numElementos, true, false, pFaceta.AlgoritmoTransformacion);
                        if (facetaItemModel != null)
                        {
                            facetaModel.FacetItemList.Add(facetaItemModel);
                        }
                    }
                }
            }

            if (elementos.Count != 1 || !elementos.ContainsKey("null"))
            {
                //Ahora se pintan el resto de elementos que el usuario no ha pinchado todavía
                List<string> elementosPintados = new List<string>();

                List<string> listaElementosOrdenados = OrdenarElementosOrdenadosTesSem(elementos, facetadoTesSemDS, arrayTesSem[2], pFaceta.TipoDisenio == TipoDisenio.ListaOrdCantidadTesauro);
                foreach (string elemento in listaElementosOrdenados)
                {
                    if (!elementosPintados.Contains(elemento))
                    {
                        elementosPintados.Add(elemento);

                        //Pinta también los hijos
                        string parametro = elemento.Replace("'", "\'");
                        string nombreReal = ObtenerPropTesSem(facetadoTesSemDS, arrayTesSem[3], elemento);
                        bool pintarX = mListaFiltros.ContainsKey(pFaceta.ClaveFaceta) && mListaFiltros[pFaceta.ClaveFaceta].Contains(elemento);

                        FacetItemModel facetaItemModel = AgregarElementoAFaceta(pFaceta.ClaveFaceta, nombreReal, parametro, elementos[elemento], pintarX, false, pFaceta.AlgoritmoTransformacion);
                        if (facetaItemModel != null)
                        {
                            facetaModel.FacetItemList.Add(facetaItemModel);
                            numElem++;
                        }
                    }
                }
            }

            if (pOrdenarPorNumero)
            {
                List<FacetItemModel> listaFacetas = new List<FacetItemModel>();
                foreach (FacetItemModel faceta in facetaModel.FacetItemList.OrderByDescending(faceta => faceta.Number))
                {
                    listaFacetas.Add(faceta);
                }
                facetaModel.FacetItemList = listaFacetas;
            }

            return facetaModel;
        }

        /// <summary>
        /// Se obtienen los tesauros solicitados. Se guardan en cache local
        /// </summary>
        /// <param name="pLimite"></param>
        /// <param name="pFaceta"></param>
        /// <param name="pFacetaDatos"></param>
        /// <param name="pOrdenarPorNumero"></param>
        /// <returns></returns>
        [NonAction]
        private FacetModel CargarFacetaTesauroSemanticoArbol(int pLimite, Faceta pFaceta, FacetadoDS pFacetaDatos, bool pOrdenarPorNumero)
        {
            string[] arrayTesSem = ObtenerDatosFacetaTesSem(pFaceta.ClaveFaceta);
            string tituloFac = ObtenerNombreFaceta(pFaceta.Nombre);

            if (mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
            {
                tituloFac = UtilCadenas.ObtenerTextoDeIdioma(pFaceta.FiltroProyectoID.Split(';')[2], mLanguageCode, ParametrosGenerales.IdiomaDefecto);
            }

            Dictionary<string, int> elementos = new Dictionary<string, int>();
            foreach (DataRow myrow in pFacetaDatos.Tables[pFaceta.ClaveFaceta].Rows)
            {
                if (!elementos.ContainsKey((string)myrow[0]))
                {
                    string nombre = (string)myrow[0];
                    if (string.IsNullOrEmpty(nombre))
                    {
                        nombre = TextoSinEspecificar;
                    }
                    int cantidad = 0;
                    int.TryParse((string)myrow[1], out cantidad);

                    elementos.Add(nombre, cantidad);
                }
            }

            FacetadoDS facetadoTesSemDS = null;

            if (elementos.Count > 0)
            {
                if (!TesauroSemDSFaceta.ContainsKey(pFaceta.ClaveFaceta) || mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
                {
                    List<string> listaPropsTesSem = new List<string>();
                    listaPropsTesSem.Add(arrayTesSem[2]);
                    listaPropsTesSem.Add(arrayTesSem[3]);
                    listaPropsTesSem.Add(arrayTesSem[4]);

                    List<string> listaEntidadesBusqueda = new List<string>();

                    if (mListaFiltros.ContainsKey(pFaceta.ClaveFaceta) && !mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
                    {
                        listaEntidadesBusqueda.AddRange(mListaFiltros[pFaceta.ClaveFaceta]);
                    }

                    listaEntidadesBusqueda.AddRange(elementos.Keys);
                    //Obtenerlo de cache siempre que se pueda, pero hacer la petición de todos los recursos.
                    FacetadoCL facetadoCL = new FacetadoCL(mUtilServicios.UrlIntragnoss, mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    facetadoTesSemDS = facetadoCL.ObtenerModeloTesauroSemanticoDeBusquedaEnProyecto(mGrafoID, pFaceta.ClaveFaceta, UtilIdiomas.LanguageCode);

                    if (facetadoTesSemDS == null)
                    {
                        FacetadoCN facCN = new FacetadoCN(mUtilServicios.UrlIntragnoss, mGrafoID, mEntityContext, mLoggingService, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                        facetadoTesSemDS = facCN.ObtenerValoresPropiedadesEntidades(mGrafoID, listaEntidadesBusqueda, listaPropsTesSem, true);
                        facCN.Dispose();

                        if (!TesauroSemDSFaceta.ContainsKey(pFaceta.ClaveFaceta))
                        {
                            TesauroSemDSFaceta.Add(pFaceta.ClaveFaceta, facetadoTesSemDS);
                        }

                        facetadoCL.AgregarTesauroSemanticoDeBusquedaEnProyecto(facetadoTesSemDS, mGrafoID, pFaceta.ClaveFaceta, UtilIdiomas.LanguageCode);
                        facetadoCL.Dispose();
                    }
                }
                else
                {
                    facetadoTesSemDS = TesauroSemDSFaceta[pFaceta.ClaveFaceta];
                }
            }

            string idPanel = NormalizarNombreFaceta(pFaceta.ClaveFaceta);
            string claveFacetaANSI = pFaceta.ClaveFaceta;
            string Titulo = tituloFac;

            //Obtengo la url a la que hay que redireccionar en caso de que estemos en la home de una comunidad
            string url = "";
            string accion = "";
            if (mFacetasHomeCatalogo)
            {
                url = ObtenerUrlPaginaActual(pFaceta);
                accion = $"document.location = '{url}?";
            }

            int limite = pLimite;

            if (elementos.Keys.Count <= pLimite + 3)
            {
                limite = pLimite + 3;
            }

            FacetModel facetaModel = new FacetModel();
            facetaModel.FacetItemList = new List<FacetItemModel>();
            //Se le da este guid para que la vista reconozca a través de el que es una faceta de tipo tesauro semántico.
            facetaModel.ThesaurusID = new Guid("11111111-1111-1111-1111-111111111111");
            facetaModel.Key = idPanel;
            facetaModel.Name = Titulo;
            facetaModel.OneFacetRequest = !string.IsNullOrEmpty(mFaceta);

            facetaModel.FacetKey = pFaceta.ClaveFaceta;
            if (GruposPorTipo && pFaceta != null && pFaceta.ClaveFaceta != "rdf:type" && !FacetasComunesGrupos.Contains(pFaceta.ClaveFaceta) && !pFaceta.ClaveFaceta.StartsWith(pFaceta.ObjetoConocimiento + ";"))
            {
                if (mFaceta != null && mFaceta.Equals(pFaceta.ClaveFaceta) && mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1)
                {
                    facetaModel.FacetKey = $"{mListaFiltrosConGrupos["default;rdf:type"][0]};{pFaceta.ClaveFaceta}";
                }
                else
                {
                    facetaModel.FacetKey = pFaceta.ObjetoConocimiento + ";" + pFaceta.ClaveFaceta;
                }
            }

            CargarConfiguracionCajaBusquedaFaceta(facetaModel, pFaceta);
            facetaModel.Order = pFaceta.Orden;
            facetaModel.Multilanguage = pFaceta.MultiIdioma.ToString().ToLower();
            facetaModel.AutocompleteBehaviour = ObtenerBehaviourDeComportamiento(pFaceta.Comportamiento);

            if (mParametroProyecto.ContainsKey(ParametroAD.VerMasFacetaTesauroSemantico) && mParametroProyecto[ParametroAD.VerMasFacetaTesauroSemantico].Equals("1"))
            {
                facetaModel.SeeMore = true;
            }

            List<string> filtrosFacetas = null;
            List<string> filtrosUsuarios = null;

            //Filtro seleccionado dentro de esta faceta.
            if (mListaFiltros.ContainsKey(pFaceta.ClaveFaceta) && !mFacetadoDSAuxPorFaceta.ContainsKey(pFaceta.ClaveFaceta))
            {
                filtrosFacetas = mListaFiltros[pFaceta.ClaveFaceta];
                filtrosUsuarios = null;
                if (mListaFiltrosFacetasUsuario.ContainsKey(pFaceta.ClaveFaceta))
                {
                    filtrosUsuarios = this.mListaFiltrosFacetasUsuario[pFaceta.ClaveFaceta];
                }

                //Se muestran primero los filtros seleccionados por el usuario con una X
                foreach (string filtro in filtrosFacetas.ToArray())
                {
                    string nombreReal = ObtenerPropTesSem(facetadoTesSemDS, arrayTesSem[3], filtro);

                    if ((mListaFiltrosFacetasUsuario.ContainsKey(pFaceta.ClaveFaceta)) && (mListaFiltrosFacetasUsuario[pFaceta.ClaveFaceta].Contains(filtro)))
                    {
                        int numElementos = -1;

                        FacetItemModel facetaItemModel = AgregarElementoAFaceta(pFaceta.ClaveFaceta, nombreReal, filtro, numElementos, true, false, pFaceta.AlgoritmoTransformacion);
                        if (facetaItemModel != null)
                        {
                            facetaModel.FacetItemList.Add(facetaItemModel);
                        }
                    }
                }
            }

            if (elementos.Count != 1 || !elementos.ContainsKey("null"))
            {
                //Ahora se pintan el resto de elementos que el usuario no ha pinchado todavía
                List<string> listaElementosOrdenados = OrdenarElementosOrdenadosTesSem(elementos, facetadoTesSemDS, arrayTesSem[2], pFaceta.TipoDisenio == TipoDisenio.ListaOrdCantidadTesauro);
                List<string> listaElementosPadre = new List<string>(listaElementosOrdenados);

                Dictionary<string, KeyValuePair<string, List<string>>> padreHijos = new Dictionary<string, KeyValuePair<string, List<string>>>();

                foreach (string elemento in listaElementosOrdenados)
                {
                    List<string> hijos = ObtenerPropTesSemList(facetadoTesSemDS, arrayTesSem[4], elemento);

                    //Por cada hijo se elimina de listaElementosPadre
                    if (hijos != null)
                    {
                        foreach (string hijo in hijos)
                        {
                            string nombreRealHijo = ObtenerPropTesSem(facetadoTesSemDS, arrayTesSem[3], hijo);
                            List<string> hijosDelHijo = ObtenerPropTesSemList(facetadoTesSemDS, arrayTesSem[4], hijo);

                            if (!padreHijos.ContainsKey(hijo))
                            {
                                padreHijos.Add(hijo, new KeyValuePair<string, List<string>>(nombreRealHijo, hijosDelHijo));
                            }

                            if (listaElementosPadre.Contains(hijo))
                            {
                                listaElementosPadre.Remove(hijo);
                            }
                        }
                    }
                    string nombreRealPadre = ObtenerPropTesSem(facetadoTesSemDS, arrayTesSem[3], elemento);

                    if (!padreHijos.ContainsKey(elemento))
                    {
                        padreHijos.Add(elemento, new KeyValuePair<string, List<string>>(nombreRealPadre, hijos));
                    }
                }

                bool estaFacetado = false;
                string[] filtroProyectoID = pFaceta.FiltroProyectoID.Split(';');
                if (filtroProyectoID.Length > 3)
                {
                    estaFacetado = true;
                    // El tesauro está facetado
                    string categoriaFacetada = filtroProyectoID[3];
                    if (padreHijos.ContainsKey(categoriaFacetada))
                    {
                        facetaModel.Name = padreHijos[categoriaFacetada].Key;
                        facetaModel.Name = TransformarTextoSegunMayusculas(facetaModel.Name, pFaceta.Mayusculas);

                        foreach (string hijo in padreHijos[categoriaFacetada].Value)
                        {
                            string filtrosPadresURL = facetaModel.FacetKey + "=" + categoriaFacetada;
                            FacetItemModel facetaItemModel = AgregarElementoArbolTesSem(pFaceta, hijo, filtrosPadresURL, padreHijos, listaElementosOrdenados, elementos, facetaModel.FacetKey, pOrdenarPorNumero);
                            if (facetaItemModel != null)
                            {
                                facetaModel.FacetItemList.Add(facetaItemModel);
                            }
                        }
                    }
                    else if (categoriaFacetada.Contains('|'))
                    {
                        // Hay varias categorías a incluir en una sola faceta
                        string[] categorias = categoriaFacetada.Split('|');
                        foreach (string categoria in categorias)
                        {
                            if (padreHijos.ContainsKey(categoria))
                            {
                                FacetItemModel facetaItemModel = AgregarElementoArbolTesSem(pFaceta, categoria, string.Empty, padreHijos, listaElementosOrdenados, elementos, facetaModel.FacetKey, pOrdenarPorNumero);
                                if (facetaItemModel != null)
                                {
                                    facetaModel.FacetItemList.Add(facetaItemModel);
                                }
                            }
                        }
                    }
                }

                if (!estaFacetado)
                {
                    limite = listaElementosPadre.Count;
                    if (facetaModel.SeeMore && pLimite < limite)
                    {
                        limite = pLimite;
                    }

                    for (int i = 0; i < limite; i++)
                    {
                        FacetItemModel facetaItemModel = AgregarElementoArbolTesSem(pFaceta, listaElementosPadre[i], string.Empty, padreHijos, listaElementosOrdenados, elementos, facetaModel.FacetKey, pOrdenarPorNumero);
                        if (facetaItemModel != null)
                        {
                            facetaModel.FacetItemList.Add(facetaItemModel);
                        }
                    }
                }
            }

            if (pOrdenarPorNumero)
            {
                List<FacetItemModel> listaFacetas = new List<FacetItemModel>();
                foreach (FacetItemModel faceta in facetaModel.FacetItemList.OrderByDescending(faceta => faceta.Number))
                {
                    listaFacetas.Add(faceta);
                }
                facetaModel.FacetItemList = listaFacetas;
            }

            return facetaModel;
        }

        [NonAction]
        private string TransformarTextoSegunMayusculas(string pNombre, FacetaMayuscula pMayusculas)
        {
            string resultado = pNombre;
            if (!string.IsNullOrEmpty(pNombre))
            {
                //Compruebo como se debe pintar el elemento
                switch (pMayusculas)
                {
                    case FacetaMayuscula.MayusculasTodasPalabras:
                        resultado = UtilCadenas.ConvertirPrimeraLetraPalabraAMayusculas(pNombre);
                        break;
                    case FacetaMayuscula.MayusculasTodoMenosArticulos:
                        resultado = UtilCadenas.ConvertirPrimeraLetraPalabraAMayusculasExceptoArticulos(pNombre);
                        break;
                    case FacetaMayuscula.MayusculasPrimeraPalabra:
                        resultado = UtilCadenas.ConvertirPrimeraLetraDeFraseAMayúsculas(pNombre);
                        break;
                    case FacetaMayuscula.MayusculasTodasLetras:
                        resultado = UtilCadenas.ConvertirAMayúsculas(pNombre);
                        break;
                }
            }

            return resultado;
        }

        [NonAction]
        private FacetItemModel AgregarElementoArbolTesSem(Faceta pFaceta, string pElementoHijo, string pFiltroPadres, Dictionary<string, KeyValuePair<string, List<string>>> pElementos, List<string> pListaElementosOrdenados, Dictionary<string, int> pElementosContadores, string pClaveFaceta, bool pOrdenarPorNumero)
        {
            if (pListaElementosOrdenados.Contains(pElementoHijo))
            {
                string idHijo = null;

                if (pElementos.Keys.Contains(pElementoHijo) && pElementos[pElementoHijo].Value != null && pElementos[pElementoHijo].Value.Count > 0)
                {
                    foreach (string a in pElementos[pElementoHijo].Value)
                    {
                        if (pListaElementosOrdenados.Contains(a))
                        {
                            idHijo = $"hijo_{Guid.NewGuid().ToString()}";
                            break;
                        }
                    }
                }

                // Los filtros que contienen & no funcionaban correctamente
                string elementoFiltro = pElementoHijo.Replace("&", "%26");
                string elementoFiltroURL = pElementoHijo.Replace("&", "%2526");

                string urlFiltro = "";
                if (!string.IsNullOrEmpty(pFiltroPadres))
                {
                    urlFiltro = ObtenerUrlFiltro($"{pFiltroPadres}&{pClaveFaceta}={elementoFiltroURL}", pFaceta, null);
                }
                else
                {
                    urlFiltro = ObtenerUrlFiltro($"{pClaveFaceta}={elementoFiltroURL}", pFaceta, null);
                }

                string nombreReal = pElementos[pElementoHijo].Key;
                nombreReal = TransformarTextoSegunMayusculas(nombreReal, pFaceta.Mayusculas);

                if (!string.IsNullOrEmpty(nombreReal))
                {
                    FacetaMayuscula mayusculas = FacetaMayuscula.Nada;
                    mayusculas = pFaceta.Mayusculas;

                    //Compruebo como se debe pintar el elemento
                    switch (mayusculas)
                    {
                        case FacetaMayuscula.MayusculasTodasPalabras:
                            nombreReal = UtilCadenas.ConvertirPrimeraLetraPalabraAMayusculas(nombreReal);
                            break;
                        case FacetaMayuscula.MayusculasTodoMenosArticulos:
                            nombreReal = UtilCadenas.ConvertirPrimeraLetraPalabraAMayusculasExceptoArticulos(nombreReal);
                            break;
                        case FacetaMayuscula.MayusculasPrimeraPalabra:
                            nombreReal = UtilCadenas.ConvertirPrimeraLetraDeFraseAMayúsculas(nombreReal);
                            break;
                        case FacetaMayuscula.MayusculasTodasLetras:
                            nombreReal = UtilCadenas.ConvertirAMayúsculas(nombreReal);
                            break;
                    }
                }

                FacetItemModel facetaItem = new FacetItemModel();
                facetaItem.Filter = urlFiltro;
                facetaItem.Number = pElementosContadores[pElementoHijo];
                facetaItem.Tittle = nombreReal;
                facetaItem.Name = $"{pClaveFaceta}={elementoFiltro}";

                facetaItem.FacetItemlist = new List<FacetItemModel>();
                if (mListaFiltros.ContainsKey(pFaceta.ClaveFaceta) && mListaFiltros[pFaceta.ClaveFaceta].Contains(pElementoHijo))
                {
                    facetaItem.Selected = true;
                }

                if (idHijo != null)
                {
                    string filtrosPadresURL = "";
                    if (!string.IsNullOrEmpty(pFiltroPadres))
                    {
                        filtrosPadresURL = $"{pFiltroPadres}&{pClaveFaceta}={pElementoHijo}";
                    }
                    else
                    {
                        filtrosPadresURL = $"{pClaveFaceta}={pElementoHijo}";
                    }

                    foreach (string hijo in pElementos[pElementoHijo].Value)
                    {
                        FacetItemModel facetaItemModel = AgregarElementoArbolTesSem(pFaceta, hijo, filtrosPadresURL, pElementos, pListaElementosOrdenados, pElementosContadores, pClaveFaceta, pOrdenarPorNumero);
                        if (facetaItemModel != null)
                        {
                            facetaItem.FacetItemlist.Add(facetaItemModel);
                        }
                    }
                }

                if (pOrdenarPorNumero)
                {
                    List<FacetItemModel> listaFacetas = new List<FacetItemModel>();
                    foreach (FacetItemModel faceta in facetaItem.FacetItemlist.OrderByDescending(faceta => faceta.Number))
                    {
                        listaFacetas.Add(faceta);
                    }
                    facetaItem.FacetItemlist = listaFacetas;
                }

                return facetaItem;
            }
            return null;
        }

        [NonAction]
        public FacetModel AgregarFaceta(string pClaveFaceta, string pTitulo, Dictionary<string, int> pElementosFaceta, Dictionary<string, string> pParametrosElementos, int pLimite, Faceta pFaceta)
        {
            return AgregarFaceta(pClaveFaceta, pTitulo, pElementosFaceta, pParametrosElementos, pLimite, pLimite, pFaceta);
        }

        [NonAction]
        public FacetModel AgregarFaceta(string pClaveFaceta, string pTitulo, Dictionary<string, int> pElementosFaceta, Dictionary<string, string> pParametrosElementos, int pLimite, int pLimiteOriginal, Faceta pFaceta)
        {
            string idPanel = NormalizarNombreFaceta(pClaveFaceta);
          
            if (pElementosFaceta != null && pElementosFaceta.Count > 0 && (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.CalendarioConRangos) || pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Calendario)))
            {
                pLimite = pElementosFaceta.Count + 1;
            }

            int limite = pLimite;

            if (pElementosFaceta.Keys.Count <= (pLimite * 2 - 1))
            {
                limite = pLimite * 2;
            }
            int numElem = 0;


            FacetModel facetaModel = new FacetModel();
            facetaModel.FacetGrouped = false;
            facetaModel.FacetItemList = new List<FacetItemModel>();
            facetaModel.ThesaurusID = Guid.Empty;
            facetaModel.Key = idPanel;
            facetaModel.Name = pTitulo;
            facetaModel.OneFacetRequest = !string.IsNullOrEmpty(mFaceta);

            string consultaReciproca, claveFaceta = string.Empty;
            mFacetadoCL.FacetadoCN.FacetadoAD.ObtenerDatosFiltroReciproco(out consultaReciproca, pFaceta.ClaveFaceta, out claveFaceta);

            if (!string.IsNullOrEmpty(consultaReciproca))
            {
                claveFaceta = $"{FacetadoAD.PARTE_FILTRO_RECIPROCO}@@@{claveFaceta}";
            }

            facetaModel.FacetKey = claveFaceta;
            if (GruposPorTipo && pFaceta != null && claveFaceta != "rdf:type" && !FacetasComunesGrupos.Contains(claveFaceta) && !pClaveFaceta.StartsWith($"{pFaceta.ObjetoConocimiento};"))
            {
                if (mFaceta != null && mFaceta.Equals(pClaveFaceta) && mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1)
                {
                    facetaModel.FacetKey = $"{mListaFiltrosConGrupos["default;rdf:type"][0]};{pClaveFaceta}";
                }
                else
                {
                    facetaModel.FacetKey = $"{pFaceta.ObjetoConocimiento};{pClaveFaceta}";
                }
            }

            CargarConfiguracionCajaBusquedaFaceta(facetaModel, pFaceta);
            facetaModel.Order = pFaceta.Orden;
            facetaModel.Multilanguage = pFaceta.MultiIdioma.ToString().ToLower();
            facetaModel.AutocompleteBehaviour = ObtenerBehaviourDeComportamiento(pFaceta.Comportamiento);

            List<string> filtrosFacetas = null;
            List<string> filtrosUsuarios = null;

            if (pElementosFaceta.Count != 1 || !pElementosFaceta.ContainsKey("null"))
            {
                //Ahora se pintan el resto de elementos que el usuario no ha pinchado todavía
                List<string> elementosPintados = new List<string>();

                Dictionary<string, List<string>> listaElementosOrdenados;

                if (GruposPorTipo && claveFaceta == "rdf:type")
                {
                    Dictionary<string, int> pElementosFacetaAux = new Dictionary<string, int>();

                    string[] tipos = FilaPestanyaBusquedaActual.CampoFiltro.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string tipo in tipos)
                    {
                        string tipoActual = tipo.Replace(claveFaceta + "=", "");

                        string elemento = ObtenerNombreTipoElemento(tipoActual);
                        if (elemento.Contains("@"))
                        {
                            elemento = ObtenerNombreFaceta(elemento);
                        }
                        if (pElementosFaceta.ContainsKey(elemento) && !pElementosFacetaAux.ContainsKey(elemento))
                        {
                            pElementosFacetaAux.Add(elemento, pElementosFaceta[elemento]);
                        }
                    }
                    pElementosFaceta = pElementosFacetaAux;
                }

                listaElementosOrdenados = OrdenarElementosFaceta(pClaveFaceta, pElementosFaceta, pParametrosElementos);

                foreach (string elementoPadre in listaElementosOrdenados.Keys)
                {
                    if (!elementosPintados.Contains(elementoPadre))
                    {
                        elementosPintados.Add(elementoPadre);

                        Dictionary<string, int> elementosIndentados = new Dictionary<string, int>();
                        elementosIndentados.Add(elementoPadre, 0);

                        foreach (string hijo in listaElementosOrdenados[elementoPadre])
                        {
                            elementosIndentados.Add(hijo, 15);
                            elementosPintados.Add(hijo);

                            if (listaElementosOrdenados.ContainsKey(hijo))
                            {
                                foreach (string nieto in listaElementosOrdenados[hijo])
                                {
                                    elementosIndentados.Add(nieto, 30);
                                }
                            }
                        }

                        foreach (string elemento in elementosIndentados.Keys)
                        {
                            //Pinta también los hijos
                            string parametro = elemento.Replace("'", "\'");

                            if (pFaceta.MultiIdioma)
                            {
                                parametro += $"@{UtilIdiomas.LanguageCode}";
                            }

                            string parametroComprobar = parametro;
                            if (parametro.Equals(TextoSinEspecificar))
                            {
                                parametroComprobar = "";
                            }

                            if ((pParametrosElementos != null) && pParametrosElementos.ContainsKey(elemento))
                            {
                                parametro = pParametrosElementos[elemento];
                                parametroComprobar = parametro;
                            }

                            if (pClaveFaceta.EndsWith(FacetaAD.Faceta_Gnoss_SubType))
                            {
                                parametroComprobar = FacetaAD.ObtenerValorAplicandoNamespaces(parametroComprobar, GestorFacetas.FacetasDW.ListaOntologiaProyecto, false);
                            }

                            if (((filtrosFacetas == null) || ((!filtrosFacetas.Contains(parametroComprobar)) && !filtrosFacetas.Contains(parametroComprobar)) || (filtrosUsuarios == null) || ((!filtrosUsuarios.Contains(parametroComprobar)) && !filtrosUsuarios.Contains(parametroComprobar)))
                                && ((filtrosFacetas == null) || !string.IsNullOrEmpty(parametroComprobar) || !filtrosFacetas.Contains(FacetadoAD.FILTRO_SIN_ESPECIFICAR)))
                            {
                                string tempParametroSinMultiIdioma = string.Empty;
                                if (pFaceta.MultiIdioma)
                                {
                                    tempParametroSinMultiIdioma = parametro.Substring(0, parametro.LastIndexOf("@"));
                                }

                                string valorReal = parametro;
                                if (pClaveFaceta.EndsWith(FacetaAD.Faceta_Gnoss_SubType))
                                {
                                    valorReal = FacetaAD.ObtenerValorAplicandoNamespaces(parametro, GestorFacetas.FacetasDW.ListaOntologiaProyecto, true);
                                }

                                if (mListaFiltrosFacetasUsuario.ContainsKey(pClaveFaceta) &&
                                    ((!string.IsNullOrEmpty(tempParametroSinMultiIdioma) && mListaFiltrosFacetasUsuario[pClaveFaceta].Contains(tempParametroSinMultiIdioma)) ||
                                    mListaFiltrosFacetasUsuario[pClaveFaceta].Contains(valorReal)))
                                {
                                    FacetItemModel facetaItemModel = AgregarElementoAFaceta(claveFaceta, elemento, parametro, pElementosFaceta[elemento], true, false, pFaceta.AlgoritmoTransformacion);
                                    if (facetaItemModel != null)
                                    {
                                        facetaModel.FacetItemList.Add(facetaItemModel);
                                    }
                                }
                                else
                                {
                                    FacetItemModel facetaItemModel = AgregarElementoAFaceta(claveFaceta, elemento, parametro, pElementosFaceta[elemento], false, 0, false, elementosIndentados[elemento], null, false, null, pFaceta.AlgoritmoTransformacion, pFaceta.TipoPropiedad);
                                    if (facetaItemModel != null)
                                    {
                                        facetaModel.FacetItemList.Add(facetaItemModel);
                                    }
                                }

                                numElem++;
                            }
                            if ((pLimite > 0) && (numElem >= limite))
                            {
                                break;
                            }
                        }

                        if ((pLimite > 0) && (numElem >= limite))
                        {
                            break;
                        }
                    }
                }
            }
            if ((pLimite > 0) && (numElem >= limite) && (!mFacetasHomeCatalogo || pFaceta.MostrarVerMas) && !mFacetasEnFormSem && !pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Rangos) && !pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Rangos))
            {
                //TODO ALVARO
                facetaModel.SeeMore = true;
            }

            if (GruposPorTipo && claveFaceta == "rdf:type")
            {
                if (GruposAgrupados.Count > 0)
                {
                    facetaModel.GroupedGroups = new Dictionary<string, List<string>>();
                    foreach (string nombreGrupo in GruposAgrupados.Keys)
                    {
                        facetaModel.GroupedGroups.Add(nombreGrupo, new List<string>());
                        foreach (string tipo in GruposAgrupados[nombreGrupo])
                        {
                            facetaModel.GroupedGroups[nombreGrupo].Add(tipo);
                        }
                    }
                }

                facetaModel.FacetGrouped = true;

                string tipoPorDefectoActual = "";
                if (mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1)
                {
                    tipoPorDefectoActual = mListaFiltrosConGrupos["default;rdf:type"][0];
                }

                foreach (FacetItemModel itemFaceta in facetaModel.FacetItemList)
                {
                    string urlActual = itemFaceta.Filter.Replace("default;rdf:type", "rdf:type");
                    string tipoActual = itemFaceta.Name.Replace("rdf:type=", "");

                    if (tipoActual != tipoPorDefectoActual)
                    {
                        itemFaceta.Selected = false;

                        if (urlActual.Contains("rdf:type=" + tipoActual))
                        {
                            //Si contiene el filtro lo ponemos por defecto
                            urlActual = urlActual.Replace("rdf:type=" + tipoActual, "default;rdf:type=" + tipoActual);
                        }
                        else
                        {
                            //Si no contiene el filtro lo ponemos por defecto
                            string filtro = "default;rdf:type=" + tipoActual;
                            if (urlActual.Contains("?"))
                            {
                                urlActual += "&" + filtro;
                            }
                            else
                            {
                                urlActual += "?" + filtro;
                            }
                        }
                    }
                    itemFaceta.Filter = urlActual;
                }
            }

            return facetaModel;
        }

        /// <summary>
        /// Devuelve el tipo de caja de búsqueda que se va a pintar en la facetamodel.
        /// </summary>
        /// <param name="pFaceta"></param>
        /// <returns>El tipo de caja para la faceta</returns>
        [NonAction]
        private void CargarConfiguracionCajaBusquedaFaceta(FacetModel pFacetaModel, Faceta pFaceta)
        {
            SearchBoxType tipoCaja = SearchBoxType.None;
            AutocompleteTypeSearchBox tipoAutocompletar = AutocompleteTypeSearchBox.None;

            if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Fechas))
            {
                tipoCaja = SearchBoxType.FromToDates;
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Rangos))
            {
                if (pFaceta.TipoDisenio.Equals(TipoDisenio.RangoSoloDesde))
                {
                    tipoCaja = SearchBoxType.FromRank;
                }
                else if (pFaceta.TipoDisenio.Equals(TipoDisenio.RangoSoloHasta))
                {
                    tipoCaja = SearchBoxType.ToRank;
                }
                else
                {
                    tipoCaja = SearchBoxType.FromToRank;
                }
            }
            else if ((pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Ninguno) || pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.MultiIdioma)) && !pFaceta.ClaveFaceta.EndsWith(FacetaAD.Faceta_Gnoss_SubType))
            {
                tipoCaja = SearchBoxType.Simple;
                tipoAutocompletar = ObtenerTipoAutocompletarCajaBusqueda(pFaceta);

            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Calendario))
            {
                tipoCaja = SearchBoxType.Calendar;
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.CalendarioConRangos))
            {
                tipoCaja = SearchBoxType.RankCalendar;
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.CategoriaArbol) && string.IsNullOrEmpty(pFaceta.FiltroProyectoID))
            {
                tipoCaja = SearchBoxType.TreeList;
            }
            else if (pFaceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.Categoria) && string.IsNullOrEmpty(pFaceta.FiltroProyectoID))
            {
                tipoCaja = SearchBoxType.ListTree;
            }

            pFacetaModel.SearchBoxType = tipoCaja;
            pFacetaModel.AutocompleteTypeSearchBox = tipoAutocompletar;
        }
        [NonAction]
        private AutocompleteTypeSearchBox ObtenerTipoAutocompletarCajaBusqueda(Faceta pFaceta)
        {
            AutocompleteTypeSearchBox tipoAutocompletar = AutocompleteTypeSearchBox.None;
            //Si se hacen búsquedas en virtuoso, no es necesario generar los tags.
            bool busquedasTagsVirtuoso = ProyectoHaceBusquedasVirtuoso(mProyectoID, mOrganizacionID);

            if (mProyectoID == ProyectoAD.MetaProyecto)
            {
                if (mTipoBusqueda == TipoBusqueda.Mensajes || mTipoBusqueda == TipoBusqueda.Comentarios || mTipoBusqueda == TipoBusqueda.Invitaciones || mTipoBusqueda == TipoBusqueda.Notificaciones)
                {
                    tipoAutocompletar = AutocompleteTypeSearchBox.AutocompleteUser;
                }
                //TODO Javier: Cambiar por leer alguna configuración:
                else if (FilaProyecto.TipoProyecto.Equals(TipoProyecto.CatalogoNoSocialConUnTipoDeRecurso) && ((mPrimeraCarga && string.IsNullOrEmpty(mFiltroContextoWhere)) || mFacetasHomeCatalogo) && (pFaceta.ClaveFaceta == "sioc_t:Tag" || pFaceta.ClaveFaceta == "dc:creator@@@foaf:name") && !busquedasTagsVirtuoso)
                {
                    tipoAutocompletar = AutocompleteTypeSearchBox.AutocompleteTipedTags;
                }
                else if (((mPrimeraCarga && string.IsNullOrEmpty(mFiltroContextoWhere)) || mFacetasHomeCatalogo) && mProyectoOrigenID == Guid.Empty && (mTipoBusqueda != TipoBusqueda.EditarRecursosPerfil && mTipoBusqueda != TipoBusqueda.Contribuciones) && !busquedasTagsVirtuoso)
                {
                    tipoAutocompletar = AutocompleteTypeSearchBox.AutocompleteTipedTags;
                }
                else
                {
                    tipoAutocompletar = AutocompleteTypeSearchBox.AutocompleteGeneric;
                }
            }
            //TODO Javier: Cambiar por leer alguna configuración:
            else if (((TipoProyecto)FilaProyecto.TipoProyecto).Equals(TipoProyecto.CatalogoNoSocialConUnTipoDeRecurso) && ((mPrimeraCarga && string.IsNullOrEmpty(mFiltroContextoWhere)) || mFacetasHomeCatalogo) && (pFaceta.ClaveFaceta == "sioc_t:Tag" || pFaceta.ClaveFaceta == "dc:creator@@@foaf:name") && pFaceta.AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemantico && pFaceta.AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado)
            {
                tipoAutocompletar = AutocompleteTypeSearchBox.AutocompleteTipedTags;
            }
            else if (((mPrimeraCarga && string.IsNullOrEmpty(mFiltroContextoWhere)) || mFacetasHomeCatalogo) && mProyectoOrigenID == Guid.Empty && pFaceta.AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemantico && pFaceta.AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado && !busquedasTagsVirtuoso)
            {
                tipoAutocompletar = AutocompleteTypeSearchBox.AutocompleteTipedTags;
            }
            else
            {
                tipoAutocompletar = AutocompleteTypeSearchBox.AutocompleteGenericWithContextFilter;
            }

            return tipoAutocompletar;
        }

        /// <summary>
        /// Devuelve si el proyecto hace búsquedas en virtuoso.
        /// </summary>
        /// <param name="pProyectoID"></param>
        /// <returns></returns>
        [NonAction]
        private bool ProyectoHaceBusquedasVirtuoso(Guid pProyectoID, Guid pOrganizacionID)
        {
            ProyectoCL paramCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
            Dictionary<string, string> dicParametros = paramCL.ObtenerParametrosProyecto(pProyectoID);
            paramCL.Dispose();

            return dicParametros.ContainsKey("ConfigBBDDAutocompletarProyecto") && short.Parse(dicParametros["ConfigBBDDAutocompletarProyecto"]) == (short)TipoBusquedasAutocompletar.Virtuoso;
        }

        [NonAction]
        private FacetItemModel AgregarElementoAFaceta(string pClaveFaceta, string pNombre, string pClaveParametro, int pNumeroResultados, bool pPintarX, bool pEsNombreContexto, TiposAlgoritmoTransformacion pTipoAlgoritmoTransformacion)
        {
            return AgregarElementoAFaceta(pClaveFaceta, pNombre, pClaveParametro, pNumeroResultados, pPintarX, 0, false, 0, null, pEsNombreContexto, null, pTipoAlgoritmoTransformacion);
        }

        [NonAction]
        private FacetItemModel AgregarElementoAFaceta(string pClaveFaceta, string pNombre, string pClaveParametro, int pNumeroResultados, bool pPintarX, int pNivel, bool pEsArbol, int pPixelesIndentado, List<string> pListaElementosExpandidos, bool pEsNombreContexto, Dictionary<string, string> pParametrosElementos, TiposAlgoritmoTransformacion pTipoAlgoritmoTransformacion, TipoPropiedadFaceta pTipoPropiedadFaceta = TipoPropiedadFaceta.NULL)
        {
            if (string.IsNullOrEmpty(pNombre))
            {
                pNombre = "";
            }

            if (pNombre.Contains(FacetadoAD.FACETA_CONDICIONADA))
            {
                pNombre = pNombre.Remove(pNombre.IndexOf(FacetadoAD.FACETA_CONDICIONADA));
            }

            if (!pEsNombreContexto)
            {
                if (pNombre.Contains("@"))
                    pNombre = ObtenerNombreFaceta(pNombre);
            }

            if (string.IsNullOrEmpty(pClaveParametro) || pClaveParametro.Equals(TextoSinEspecificar))
            {
                pClaveParametro = FacetadoAD.FILTRO_SIN_ESPECIFICAR;
            }

            #region SubType

            if (pClaveFaceta.EndsWith(FacetaAD.Faceta_Gnoss_SubType))
            {
                pNombre = FacetaAD.ObtenerTextoSubTipoDeIdioma(pNombre, GestorFacetas.FacetasDW.ListaOntologiaProyecto, mLanguageCode);
                pClaveParametro = FacetaAD.ObtenerValorAplicandoNamespaces(pClaveParametro, GestorFacetas.FacetasDW.ListaOntologiaProyecto, false);
            }

            #endregion

            if (pPintarX && string.IsNullOrEmpty(pClaveParametro))
            {
                pClaveParametro = TextoSinEspecificar;
            }

            if (!string.IsNullOrEmpty(pNombre))
            {
                FacetaMayuscula mayusculas = FacetaMayuscula.Nada;

                string claveFacetaSinTipo = pClaveFaceta;
                if (pClaveFaceta.Contains(";"))
                {
                    claveFacetaSinTipo = claveFacetaSinTipo.Substring(claveFacetaSinTipo.IndexOf(";") + 1);
                }

                if (GestorFacetasOriginal.ListaFacetasPorClave.ContainsKey(claveFacetaSinTipo))
                {
                    mayusculas = GestorFacetasOriginal.ListaFacetasPorClave[claveFacetaSinTipo].Mayusculas;
                }

                //Compruebo como se debe pintar el elemento
                switch (mayusculas)
                {
                    case FacetaMayuscula.MayusculasTodasPalabras:
                        pNombre = UtilCadenas.ConvertirPrimeraLetraPalabraAMayusculas(pNombre);
                        break;
                    case FacetaMayuscula.MayusculasTodoMenosArticulos:
                        pNombre = UtilCadenas.ConvertirPrimeraLetraPalabraAMayusculasExceptoArticulos(pNombre);
                        break;
                    case FacetaMayuscula.MayusculasPrimeraPalabra:
                        pNombre = UtilCadenas.ConvertirPrimeraLetraDeFraseAMayúsculas(pNombre);
                        break;
                    case FacetaMayuscula.MayusculasTodasLetras:
                        pNombre = UtilCadenas.ConvertirAMayúsculas(pNombre);
                        break;
                }
            }

            Faceta faceta = null;
            if (GestorFacetas.ListaFacetasPorClave.ContainsKey(pClaveFaceta))
            {
                faceta = GestorFacetas.ListaFacetasPorClave[pClaveFaceta];
            }


            if (faceta != null && faceta.MultiIdioma && !faceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.TesauroSemantico) && !faceta.AlgoritmoTransformacion.Equals(TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado) && !pClaveParametro.EndsWith("@" + UtilIdiomas.LanguageCode))
            {
                pClaveParametro += "@" + UtilIdiomas.LanguageCode;
            }

            string urlFiltro = "";
            string nameFiltro = "";
            if (mTipoBusqueda == TipoBusqueda.Mensajes && pClaveFaceta == "dce:type")
            {
                string url = ObtenerUrlPaginaActual(faceta);
                if (url != null && url.Contains('?'))
                {
                    urlFiltro = url.Substring(0, url.IndexOf("?"));
                }
                else
                {
                    urlFiltro = $"/{UtilIdiomas.GetText("URLSEM", "MENSAJES")}";
                }

                string bandeja = "recibidos";
                pNombre = GetText("BANDEJAENTRADA", "RECIBIDOS");

                if (pClaveParametro == "Enviados")
                {
                    bandeja = "enviados";
                    pNombre = GetText("BANDEJAENTRADA", "ENVIADOS");
                }
                else if (pClaveParametro == "Eliminados")
                {
                    bandeja = "eliminados";
                    pNombre = GetText("BANDEJAENTRADA", "ELIMINADOS");
                }

                urlFiltro = $"{urlFiltro}?{bandeja}";
            }
            else
            {
                if (GruposPorTipo && faceta != null && faceta.ClaveFaceta != "rdf:type" && !FacetasComunesGrupos.Contains(faceta.ClaveFaceta) && !pClaveFaceta.StartsWith(faceta.ObjetoConocimiento + ";"))
                {
                    if (mFaceta != null && mFaceta.Equals(pClaveFaceta) && mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1)
                    {
                        pClaveFaceta = $"{mListaFiltrosConGrupos["default;rdf:type"][0]};{pClaveFaceta}";
                    }
                    else
                    {
                        pClaveFaceta = $"{faceta.ObjetoConocimiento};{pClaveFaceta}";
                    }
                }

                string claveParametro = pClaveParametro.Replace("&", "%26");
                string claveParametroURL = pClaveParametro.Replace("&", "%2526");
                urlFiltro = ObtenerUrlFiltro(pClaveFaceta + "=" + claveParametroURL, faceta, pParametrosElementos);

                if (mTipoBusqueda == TipoBusqueda.Mensajes && string.IsNullOrEmpty(mUrlPagina))
                {
                    urlFiltro = $"/{UtilIdiomas.GetText("URLSEM", "MENSAJES")}{urlFiltro}";
                }

                nameFiltro = $"{pClaveFaceta}={claveParametro}";
                string nameFiltroURL = $"{pClaveFaceta}={claveParametroURL}";

                if (pPintarX && GruposPorTipo && pNumeroResultados == -1)
                {
                    if (faceta != null && faceta.ClaveFaceta == "rdf:type" && mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1 && mListaFiltrosConGrupos["default;rdf:type"][0] == pClaveParametro)
                    {
                        urlFiltro = urlFiltro.Replace($"default;{nameFiltroURL}", "").Trim('&');
                        urlFiltro = urlFiltro.Replace(nameFiltroURL, "").Trim('&');
                        nameFiltro = $"default;{nameFiltro}";
                    }
                    else
                    {
                        urlFiltro = urlFiltro.Replace(nameFiltroURL, "").Trim('&');
                    }
                }
            }

            FacetItemModel facetaItem = new FacetItemModel();
            facetaItem.Filter = urlFiltro;
            facetaItem.Number = pNumeroResultados;
            if (pNombre.StartsWith("<>"))
            {
                facetaItem.Tittle = pNombre.Replace("<>", "").Replace("*", "");
            }
            else if (pNombre.StartsWith(">>"))
            {
                facetaItem.Tittle = pNombre.Replace(">>", "");
            }
            else
            {
                facetaItem.Tittle = pNombre;
            }
            if (pTipoAlgoritmoTransformacion == TiposAlgoritmoTransformacion.Booleano)
            {
                if (facetaItem.Tittle.ToLower() == "true")
                {
                    facetaItem.Tittle = GetText("COMMON", "SI");
                }
                else
                {
                    facetaItem.Tittle = GetText("COMMON", "NO");
                }
            }

            facetaItem.Name = nameFiltro;
            facetaItem.FacetItemlist = new List<FacetItemModel>();

            if (pPintarX)
            {
                facetaItem.Selected = true;
            }
            return facetaItem;
        }

        [NonAction]
        private int PasarAEntero(string pCadena)
        {
            int entero = 0;

            if (pCadena.Contains(','))
            {
                float numero = 0;
                float.TryParse(pCadena, out numero);
                entero = (int)numero;
            }
            else
            {
                int.TryParse(pCadena, out entero);
            }
            return entero;
        }

        [NonAction]
        private void AgregarCategoriasHijasALista(CategoriaTesauro pCategoria, List<Guid> pListaCategorias)
        {
            if (!pListaCategorias.Contains(pCategoria.Clave))
            {
                pListaCategorias.Add(pCategoria.Clave);

                foreach (CategoriaTesauro catHija in pCategoria.Hijos)
                {
                    AgregarCategoriasHijasALista(catHija, pListaCategorias);
                }
            }
        }

        /// <summary>
        /// Ordena los elementos de la faceta.
        /// </summary>
        /// <param name="pElementosFaceta">Elementos que contine la faceta</param>
        /// <param name="pFacetadoDS">DataSet con los datos del tesauro semántico</param>
        /// <param name="pPropOrden">Propiedad de orden del tesauro semántico</param>
        /// <returns>Un diccionario con las claves de cada elemento</returns>
        [NonAction]
        public List<string> OrdenarElementosOrdenadosTesSem(Dictionary<string, int> pElementosFaceta, FacetadoDS pFacetadoDS, string pPropOrden, bool pOrdenarPorCantidad)
        {
            List<string> elementoOrdenados = new List<string>();
            if (pOrdenarPorCantidad)
            {
                SortedDictionary<int, List<string>> listaOrdenada = new SortedDictionary<int, List<string>>();
                foreach (string elemento in pElementosFaceta.Keys)
                {
                    if (!listaOrdenada.ContainsKey(pElementosFaceta[elemento]))
                    {
                        listaOrdenada.Add(pElementosFaceta[elemento], new List<string>());
                    }
                    listaOrdenada[pElementosFaceta[elemento]].Add(elemento);
                }

                List<List<string>> listaAux = new List<List<string>>(listaOrdenada.Values);
                listaAux.Reverse();

                foreach (List<string> listaAuxIn in listaAux)
                {
                    elementoOrdenados.AddRange(listaAuxIn);
                }
            }
            else
            {
                SortedDictionary<string, List<string>> listaOrdenada = new SortedDictionary<string, List<string>>();

                foreach (string elemento in pElementosFaceta.Keys)
                {
                    string orden = ObtenerPropTesSem(pFacetadoDS, pPropOrden, elemento);

                    if (!string.IsNullOrEmpty(orden))
                    {
                        if (!listaOrdenada.ContainsKey(orden))
                        {
                            listaOrdenada.Add(orden, new List<string>());
                        }
                        listaOrdenada[orden].Add(elemento);
                    }
                }

                foreach (List<string> listaAux in listaOrdenada.Values)
                {
                    elementoOrdenados.AddRange(listaAux);
                }
            }

            return elementoOrdenados;
        }

        /// <summary>
        /// Obtiene el valor de una propiedad de un elemento de un tesauro semántico.
        /// </summary>
        /// <param name="pFacetadoDS">DataSet con el tesauro semántico</param>
        /// <param name="pPropiedad">Propiedad a recuperar</param>
        /// <returns>Valor de una propiedad de un elemento de un tesauro semántico</returns>
        [NonAction]
        private string ObtenerPropTesSem(FacetadoDS pFacetadoDS, string pPropiedad, string pElemento)
        {
            DataRow[] filas = pFacetadoDS.Tables[0].Select($"s='{pElemento}' AND p='{pPropiedad}'");

            if (filas.Length > 0)
            {
                if (filas.Length > 1 && pFacetadoDS.Tables[0].Columns.Count > 3 && !string.IsNullOrEmpty(mLanguageCode))
                {
                    DataRow filaSinIdioma = null;

                    foreach (DataRow fila in filas)
                    {
                        if (!string.IsNullOrEmpty((string)fila[3]))
                        {
                            if ((string)fila[3] == mLanguageCode)
                            {
                                return (string)fila[2];
                            }
                        }
                        else
                        {
                            filaSinIdioma = fila;
                        }
                    }

                    if (filaSinIdioma != null)
                    {
                        return (string)filaSinIdioma[2];
                    }
                }

                return (string)filas[0][2];
            }

            return null;
        }

        /// <summary>
        /// Obtiene el valor de una propiedad de un elemento de un tesauro semántico.
        /// </summary>
        /// <param name="pFacetadoDS">DataSet con el tesauro semántico</param>
        /// <param name="pPropiedad">Propiedad a recuperar</param>
        /// <returns>Valor de una propiedad de un elemento de un tesauro semántico</returns>
        [NonAction]
        private List<string> ObtenerPropTesSemList(FacetadoDS pFacetadoDS, string pPropiedad, string pElemento)
        {
            DataRow[] filas = pFacetadoDS.Tables[0].Select($"s='{pElemento}' AND p='{pPropiedad}'");
            List<string> listaPropiedadesSemanticas = new List<string>();
            foreach (DataRow dr in filas)
            {
                if (!listaPropiedadesSemanticas.Contains((string)dr[2]))
                {
                    listaPropiedadesSemanticas.Add((string)dr[2]);
                }
            }

            if (listaPropiedadesSemanticas.Count > 0)
            {
                return listaPropiedadesSemanticas;
            }

            return null;
        }

        [NonAction]
        private int rangoaproximado(int rango)
        {
            int rangoaproximado = 0;
            if (rango < 10)
            {
                rangoaproximado = rango;
            }
            if (rango >= 10 && rango < 1000)
            {
                while (rango % 10 != 0) { rango = rango - 1; }
                rangoaproximado = rango;
            }


            if (rango >= 1000 && rango < 10000)
            {

                while (rango % 100 != 0) { rango = rango - 1; }
                rangoaproximado = rango;
            }
            if (rango >= 10000 && rango < 100000)
            {

                while (rango % 1000 != 0) { rango = rango - 1; }
                rangoaproximado = rango;
            }

            if (rango >= 100000 && rango < 1000000)
            {

                while (rango % 10000 != 0) { rango = rango - 1; }
                rangoaproximado = rango;
            }
            return rangoaproximado;
        }

        [NonAction]
        private List<int> CalcularRangos(int pMax, int pMin)
        {
            List<int> resultado = new List<int>();

            resultado.Add(2010);
            resultado.Add(2011);
            resultado.Add(2012);
            resultado.Add(2013);

            return resultado;
        }

        #endregion

        #region Agregar facetas
        [NonAction]
        private string NormalizarNombreFaceta(string pNombreFaceta)
        {
            return pNombreFaceta.Replace(":", "_").Replace("@", "-");
        }
        [NonAction]
        public string ObtenerUrlPaginaActual(Faceta pFaceta)
        {
            string url = mUrlPagina;
            if (mFacetasHomeCatalogo)
            {
                url = $"/{GetText("URLSEM", "RECURSOS")}";

                if (pFaceta != null && (((pFaceta.FilaElementoEntity is FacetaHome || pFaceta.FilaElementoEntity is FacetaFiltroHome) && !string.IsNullOrEmpty(pFaceta.PestanyaFaceta)) || !string.IsNullOrEmpty(mPestanyaFacetaCMS)))
                {
                    if (!string.IsNullOrEmpty(pFaceta.PestanyaFaceta) || !string.IsNullOrEmpty(mPestanyaFacetaCMS))
                    {
                        string pagina = String.Empty;

                        if (!string.IsNullOrEmpty(mPestanyaFacetaCMS))
                        {
                            pagina = mPestanyaFacetaCMS;
                        }
                        else if (!string.IsNullOrEmpty(pFaceta.PestanyaFaceta))
                        {
                            pagina = pFaceta.PestanyaFaceta;
                        }

                        if (pagina == "busqueda")
                        {
                            pagina = GetText("URLSEM", "BUSQUEDAAVANZADA");
                        }
                        else if (pagina == "recursos")
                        {
                            pagina = GetText("URLSEM", "RECURSOS");
                        }
                        else if (pagina == "debates")
                        {
                            pagina = GetText("URLSEM", "DEBATES");
                        }
                        else if (pagina == "preguntas")
                        {
                            pagina = GetText("URLSEM", "PREGUNTAS");
                        }
                        else if (pagina == "encuestas")
                        {
                            pagina = GetText("URLSEM", "ENCUESTAS");
                        }
                        else if (pagina == "personas-y-organizaciones")
                        {
                            pagina = GetText("URLSEM", "PERSONASYORGANIZACIONES");
                        }

                        url = $"/{pagina}";
                    }
                }
            }
            return url;
        }

        /// <summary>
        /// Obtiene la url de la página de búsqueda.
        /// </summary>
        /// <param name="pClaveFaceta">Calve de la faceta que se está pintando</param>
        /// <returns>Url de la página de búsqueda</returns>
        [NonAction]
        private string ObtenerUrlPagina(string pUrlBase, string pClaveFaceta)
        {
            string url = mControladorBase.UrlsSemanticas.GetURLBaseRecursos(pUrlBase, UtilIdiomas, FilaProyecto.NombreCorto, "/", false);
            if (mParametros_adiccionales != "")
            {
                Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.ProyectoPestanyaBusqueda filaPestaña = PestanyasProyectoDW.ListaProyectoPestanyaBusqueda.FirstOrDefault(proy => proy.CampoFiltro.Equals(mParametros_adiccionales));
                url = mControladorBase.UrlsSemanticas.ObtenerURLComunidad(UtilIdiomas, pUrlBase, FilaProyecto.NombreCorto);
                url += $"/{filaPestaña.ProyectoPestanyaMenu.Ruta}";
            }
            return url;
        }

        /// <summary>
        /// Obtiene la url para un filtro de una faceta
        /// </summary>
        /// <param name="pFiltro">Filtro</param>
        /// <returns>Url con el filtro de una faceta</returns>
        [NonAction]
        private string ObtenerUrlFiltro(string pFiltro, Faceta pFaceta, Dictionary<string, string> pParametrosElementos)
        {
            string url = ObtenerUrlPaginaActual(pFaceta);

            if (mEsBot)
            {
                if (url != null && url.Contains('?'))
                {
                    url = url.Substring(0, url.IndexOf("?"));
                }

                if (url.Contains($"/{GetText("URLSEM", "COMUNIDAD")}/"))
                {
                    string urlAux = url;
                    url = url.Substring(0, url.IndexOf($"/{GetText("URLSEM", "COMUNIDAD")}/") + ($"/{GetText("URLSEM", "COMUNIDAD")}/").Length);
                    urlAux = urlAux.Substring(urlAux.IndexOf($"/{GetText("URLSEM", "COMUNIDAD")}/") + ($"/{GetText("URLSEM", "COMUNIDAD")}/").Length);
                    string[] urlAuxArray = urlAux.Split('/');

                    if (urlAuxArray[1] == GetText("URLSEM", "CATEGORIA"))
                    {
                        url += urlAuxArray[0];

                        if (!pFiltro.Contains("skos:ConceptID="))
                        {
                            url += $"/{GetText("URLSEM", "BUSQUEDAAVANZADA")}";
                        }
                    }
                    else
                    {
                        url += urlAuxArray[0];

                        if (urlAuxArray[1] != GetText("URLSEM", "BUSQUEDAAVANZADA") || !pFiltro.Contains("skos:ConceptID="))
                        {
                            url += $"/{urlAuxArray[1]}";
                        }
                    }
                }

                if (pFiltro.Contains("sioc_t:Tag="))
                {
                    url += $"/{GetText("URLSEM", "TAG")}/{pFiltro.Replace("sioc_t:Tag=", "")}";
                }
                else if (pFiltro.Contains("skos:ConceptID=") && pParametrosElementos != null && pParametrosElementos.ContainsKey(pFiltro.Replace("skos:ConceptID=", "")))
                {

                    url += $"/{GetText("URLSEM", "CATEGORIA")}/{UtilCadenas.EliminarCaracteresUrlSem(pParametrosElementos[pFiltro.Replace("skos:ConceptID=", "")])}/{pFiltro.Replace("skos:ConceptID=gnoss:", "")}";
                }
                else
                {
                    url += $"?{pFiltro}";
                }
            }
            else
            {
                if (url != null && url.Contains("?"))
                {
                    string[] filtros = url.Substring(url.IndexOf("?") + 1).Split('&');
                    url = url.Substring(0, url.IndexOf("?") + 1);

                    foreach (string filtro in filtros)
                    {
                        if (!filtro.Contains("pagina="))
                        {
                            url += $"{HttpUtility.UrlDecode(filtro)}&";
                        }
                    }

                    url = url.Substring(0, url.Length - 1);
                }

                CultureInfo culture = Thread.CurrentThread.CurrentCulture;
                if (pFiltro.Contains('=') && url != null && culture.CompareInfo.IndexOf(url, pFiltro, CompareOptions.IgnoreCase) >= 0)
                {
                    if (pFaceta != null && pFaceta.AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemantico && pFaceta.AlgoritmoTransformacion != TiposAlgoritmoTransformacion.TesauroSemanticoOrdenado)
                    {
                        if (culture.CompareInfo.IndexOf(url, $"{pFiltro}&", CompareOptions.IgnoreCase) >= 0)
                        {
                            url = RemplazoInsensitivo(url, $"{pFiltro}&", "");
                        }
                        else if (culture.CompareInfo.IndexOf(url, $"&{pFiltro}", CompareOptions.IgnoreCase) >= 0 && url.Substring(culture.CompareInfo.IndexOf(url, $"&{pFiltro}", CompareOptions.IgnoreCase) + $"&{pFiltro}".Length).Length == 0)
                        {   
                            url = RemplazoInsensitivo(url, $"&{pFiltro}", "");
                        }
                        else if (culture.CompareInfo.IndexOf(url, $"?{pFiltro}", CompareOptions.IgnoreCase) >= 0 && url.Substring(culture.CompareInfo.IndexOf(url, $"?{pFiltro}", CompareOptions.IgnoreCase) + $"?{pFiltro}".Length).Length == 0)
                        {
                            url = RemplazoInsensitivo(url, $"?{pFiltro}", "");
                        }
                        else
                        {
                            // No se ha remplazado, llamar al método alternativo:
                            url = ObtenerComposicionUrlGenerica(url, pFiltro, pParametrosElementos, culture);
                        }
                    }
                    else
                    {
                        string urlAux = url;
                        url = url.Substring(0, url.IndexOf("?") + 1);
                        urlAux = urlAux.Substring(url.IndexOf("?") + 1);

                        string claveFaceta = null;
                        if (pFaceta != null)
                        {
                            claveFaceta = pFaceta.ClaveFaceta;
                        }
                        else if (!string.IsNullOrEmpty(pFiltro) && pFiltro.Contains('='))
                        {
                            claveFaceta = pFiltro.Substring(0, pFiltro.IndexOf('='));
                        }

                        if (!string.IsNullOrEmpty(claveFaceta))
                        {
                            foreach (string filtroAux in urlAux.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (!filtroAux.StartsWith(claveFaceta))
                                {
                                    url += $"{filtroAux}&";
                                }
                            }
                        }

                        url = url.Substring(0, url.Length - 1);
                    }
                }
                else
                {
                    url = ObtenerComposicionUrlGenerica(url, pFiltro, pParametrosElementos, culture);
                }
            }

            return url;
        }

        [NonAction]
        private string ObtenerComposicionUrlGenerica(string pUrl, string pFiltro, Dictionary<string, string> pParametrosElementos, CultureInfo pCulture)
        {
            string tempFiltro = pFiltro;

            if (tempFiltro.Contains("sioc_t:Tag="))
            {
                tempFiltro = $"/{GetText("URLSEM", "TAG")}/{tempFiltro.Replace("sioc_t:Tag=", "")}";
            }
            else if (tempFiltro.Contains("skos:ConceptID=") && pParametrosElementos != null && pParametrosElementos.ContainsKey(tempFiltro.Replace("skos:ConceptID=", "")))
            {

                tempFiltro = $"/{GetText("URLSEM", "CATEGORIA")}/{UtilCadenas.EliminarCaracteresUrlSem(pParametrosElementos[tempFiltro.Replace("skos:ConceptID=", "")])}/{tempFiltro.Replace("skos:ConceptID=gnoss:", "")}";
            }
            else
            {
                tempFiltro = "?" + tempFiltro;
            }

            string separador = "?";
            if (pUrl != null && pUrl.Contains('?'))
            {
                separador = "&";
            }
            pUrl += separador + pFiltro;

            return pUrl;
        }

        [NonAction]
        public string RemplazoInsensitivo(string pCadenaBuscar, string pOriginal, string pReplace)
        {
            int index = pCadenaBuscar.IndexOf(pOriginal, StringComparison.OrdinalIgnoreCase);
            bool match = index >= 0;

            if (match)
            {
                pCadenaBuscar = pCadenaBuscar.Remove(index, pOriginal.Length);
                pCadenaBuscar = pCadenaBuscar.Insert(index, pReplace);
            }

            return pCadenaBuscar;
        }

        #endregion

        #region Trazas

        private void IniciarTraza()
        {
            GnossCacheCL gnossCacheCL = new GnossCacheCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            object trazaHabilitada = gnossCacheCL.ObtenerDeCache($"traza{mControladorBase.DominoAplicacion}");//72horas

            if (trazaHabilitada != null && (bool)trazaHabilitada)
            {
                LoggingService.TrazaHabilitada = true;
            }
            else
            {
                LoggingService.TrazaHabilitada = false;
            }
        }

        private void GuardarTraza()
        {
            mLoggingService.GuardarTraza(ObtenerRutaTraza());
        }

        private string ObtenerRutaTraza()
        {
            string ruta = Path.Combine(mEnv.ContentRootPath, "trazas");
            if (!string.IsNullOrEmpty(mControladorBase.DominoAplicacion))
            {
                ruta += $"\\{mControladorBase.DominoAplicacion}";
                if (!Directory.Exists(ruta))
                {
                    Directory.CreateDirectory(ruta);
                }
            }
            ruta += $"\\traza_{DateTime.Now.ToString("yyyy-MM-dd")}.txt";

            return ruta;
        }

        #endregion

        /// <summary>
        /// Carga el gestor de facetas
        /// </summary>
        [NonAction]
        private void CargarGestorFacetas()
        {
            FacetaCL facetaCL = new FacetaCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
            DataWrapperFacetas facetaDW;

            facetaDW = facetaCL.ObtenerFacetasDeProyecto(null, mOrganizacionID, mProyectoID, mFacetasHomeCatalogo, mListaFiltros);

            //Hago una copia porque el DataSet va a ser modificado y es el mismo para todas las peticiones
            facetaDW = facetaDW.Copy();
            mLoggingService.AgregarEntrada("Data set copiado");
            mGestorFacetas = new GestionFacetas(facetaDW, mLoggingService);

            // Si en algún proyecto no se quieren sacar facetas de múltiples recursos, usar el parámetro ParametroAD.OcultarFacetatasDeOntologiasEnRecursosCuandoEsMultiple
            mGestorFacetas.MontarFacetasHome = mFacetasHomeCatalogo && string.IsNullOrEmpty(mPestanyaFacetaCMS);
            GestorFacetasOriginal = new GestionFacetas(mGestorFacetas.FacetasDW.Copy(), mLoggingService);
            GestorFacetasOriginal.MontarFacetasHome = mGestorFacetas.MontarFacetasHome;
        }

        [NonAction]
        private void EstablecerOrganizacionIDDeProyectoID(Guid pProyectoID)
        {
            if (!mListaOrganizacionIDPorProyectoID.ContainsKey(pProyectoID))
            {
                ProyectoCN proyCN = new ProyectoCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                Guid orgID = proyCN.ObtenerOrganizacionIDProyecto(mProyectoID);
                proyCN.Dispose();

                if (!orgID.Equals(Guid.Empty))
                {
                    mOrganizacionID = orgID;
                }

                try
                {
                    mListaOrganizacionIDPorProyectoID.TryAdd(pProyectoID, mOrganizacionID);
                }
                catch (Exception)
                {

                }
            }
            else
            {
                mOrganizacionID = mListaOrganizacionIDPorProyectoID[pProyectoID];
            }
        }

        #region Tesauro Semántico

        /// <summary>
        /// Obtiene los datos de la faceta de tesauro semántica.
        /// </summary>
        /// <param name="pFaceta">Faceta</param>
        /// <returns>Array con la configuración: Grafo, Propiedad de unión de colección con categorías raíz, Propiedad con el Identificador de las categorías, Propiedad con el Nombre de las categorías, Propiedad que relaciona categorías padres con hijas</returns>
        [NonAction]
        public string[] ObtenerDatosFacetaTesSem(string pFaceta)
        {
            //TODO JAVIER: Leer de XML de ontología y poner en cada caso las propiedades y grafo correctos.

            string[] array = new string[7];
            array[0] = "taxonomy.owl";
            array[1] = "http://www.w3.org/2008/05/skos#member";
            array[2] = "http://purl.org/dc/elements/1.1/identifier";
            array[3] = "http://www.w3.org/2008/05/skos#prefLabel";
            array[4] = "http://www.w3.org/2008/05/skos#narrower";
            array[5] = "http://purl.org/dc/elements/1.1/source";
            array[6] = "http://www.w3.org/2008/05/skos#symbol";

            return array;
        }

        #endregion

        /// <summary>
        /// Devuelve un diccionario con las facetas que son excluyentes. Servicio Resultados.
        /// </summary>
        /// <returns></returns>
        [NonAction]
        private Dictionary<string, bool> ObtenerDiccionarioFacetasExcluyentes()
        {
            Dictionary<string, bool> dic = new Dictionary<string, bool>();
            foreach (Faceta fac in GestorFacetas.ListaFacetas)
            {
                if (!dic.ContainsKey(fac.ClaveFaceta))
                {
                    dic.Add(fac.ClaveFaceta, fac.Excluyente);
                }
            }

            return dic;
        }
        
        [NonAction]
        private AutocompleteBehaviours ObtenerBehaviourDeComportamiento(TipoMostrarSoloCaja pComportamiento)
        {
            AutocompleteBehaviours comportamientoAutocompletar = AutocompleteBehaviours.Default;

            if (pComportamiento.Equals(TipoMostrarSoloCaja.SoloCajaSiempre) || (pComportamiento.Equals(TipoMostrarSoloCaja.SoloCajaPrimeraPagina) && mPrimeraCarga))
            {
                comportamientoAutocompletar = AutocompleteBehaviours.OnlyTextBox;
            }

            return comportamientoAutocompletar;
        }

        [NonAction]
        private void MontarFiltroItemFaceta(FacetItemModel pItemFaceta, string pUrlBaseFacetas)
        {
            if (!pItemFaceta.Filter.StartsWith("http://") && !pItemFaceta.Filter.StartsWith("https://"))
            {
                //itemFaceta.Filter = urlBaseFacetas + itemFaceta.Filter;
                if (pItemFaceta.Filter.StartsWith("?"))
                {
                    if (mUrlPagina.Contains("?"))
                    {
                        pItemFaceta.Filter = pItemFaceta.Filter.Replace("?", "&");
                    }
                    pItemFaceta.Filter = mUrlPagina + pItemFaceta.Filter;
                }
                pItemFaceta.Filter = pUrlBaseFacetas + pItemFaceta.Filter;
            }

            foreach (FacetItemModel itemFaceta in pItemFaceta.FacetItemlist)
            {
                MontarFiltroItemFaceta(itemFaceta, pUrlBaseFacetas);
            }
        }

        #endregion

        #region Métodos auxiliares para Agrupaciones por tipo

        /// <summary>
        /// Preparamos los filtros para que aparezcan en el panel de limpar filtros
        /// </summary>
        /// <param name="pListaFiltros"></param>
        [NonAction]
        private void PrepararFiltrosParaFacetasAgrupadas(ref Dictionary<string, List<string>> pListaFiltros)
        {
            mListaFiltrosFacetasNombreReal = mListaFiltrosConGrupos;
            pListaFiltros = mListaFiltrosConGrupos;
            mListaFiltrosFacetasUsuario = mListaFiltrosFacetasUsuarioConGrupos;

            if (mListaFiltrosFacetasNombreReal.ContainsKey("default;rdf:type"))
            {
                if (mListaFiltrosFacetasNombreReal.ContainsKey("rdf:type"))
                {
                    mListaFiltrosFacetasNombreReal["rdf:type"].AddRange(mListaFiltrosFacetasNombreReal["default;rdf:type"]);
                }
                else
                {
                    mListaFiltrosFacetasNombreReal.Add("rdf:type", mListaFiltrosFacetasNombreReal["default;rdf:type"]);
                }
                mListaFiltrosFacetasNombreReal.Remove("default;rdf:type");
            }
            if (pListaFiltros.ContainsKey("default;rdf:type"))
            {
                if (pListaFiltros.ContainsKey("rdf:type"))
                {
                    pListaFiltros["rdf:type"].AddRange(pListaFiltros["default;rdf:type"]);
                }
                else
                {
                    pListaFiltros.Add("rdf:type", pListaFiltros["default;rdf:type"]);
                }
                pListaFiltros.Remove("default;rdf:type");
            }
            if (mListaFiltrosFacetasUsuario.ContainsKey("default;rdf:type"))
            {
                if (mListaFiltrosFacetasUsuario.ContainsKey("rdf:type"))
                {
                    mListaFiltrosFacetasUsuario["rdf:type"].AddRange(mListaFiltrosFacetasUsuario["default;rdf:type"]);
                }
                else
                {
                    mListaFiltrosFacetasUsuario.Add("rdf:type", mListaFiltrosFacetasUsuario["default;rdf:type"]);
                }
                mListaFiltrosFacetasUsuario.Remove("default;rdf:type");
            }
        }

        /// <summary>
        /// Seleccionamos las facetas que se muestran cuando estan agrupadas
        /// </summary>
        /// <param name="pNumeroFacetas"></param>
        /// <param name="inicio"></param>
        /// <param name="fin"></param>
        [NonAction]
        private void AjustarFacetasAgrupadas(int pNumeroFacetas, out int inicio, out int fin, ref List<Faceta> pListaFacetas)
        {
            //Si se agrupa por tipos y se especifica un tipo en particular se traen las facetas de la siguiente manera
            //1.- la primera peticion el rdf:type
            //2.- la segunda llamada las 3 siguientes facetas
            //3.- la tercera llamada las facetas restantes

            //Si se agrupa por tipos y no se especifica un tipo en particular solo se trae la faceta (rdf:type)

            inicio = 0;
            fin = pNumeroFacetas;

            //Ajuste de pListaFacetas
            if (pNumeroFacetas == 1)
            {
                //Si es la primera petición solo nos interesa el "rdf:type"
                pListaFacetas = pListaFacetas.Where(faceta => faceta.ClaveFaceta == "rdf:type").ToList();
                inicio = 0;
                fin = 1;
            }
            else
            {
                //Si no es la primera petición no nos interesa el "rdf:type"
                pListaFacetas = pListaFacetas.Where(faceta => faceta.ClaveFaceta != "rdf:type").ToList();
                inicio = 0;
                fin = 0;
            }

            if (mListaFiltrosConGrupos.ContainsKey("default;rdf:type") && mListaFiltrosConGrupos["default;rdf:type"].Count == 1)
            {
                if (pNumeroFacetas == 2)
                {
                    inicio = 0;
                    fin = pListaFacetas.Count;
                    if (fin > 3)
                    {
                        fin = 3;
                    }
                }
                else if (pNumeroFacetas == 3)
                {
                    inicio = 3;
                    fin = pListaFacetas.Count;
                }
            }

            if (pListaFacetas.Count == 0)
            {
                inicio = 0;
                fin = 0;
            }
        }

        /// <summary>
        /// Ajustamos los filtros
        /// </summary>
        [NonAction]
        private void AjustarFiltrosParaFacetasAgrupadas(ref string pParametros)
        {
            pParametros = pParametros.Replace("default;rdf:type", "rdf:type");

            //Tanto en mListaFiltros como en mListaFiltrosFacetasUsuario vienen las facetas con el tipo.
            //Eliminamos el tipo de las facetas y nos quedamos con los filtros que afecten al rdf:type en el que estamos
            //En las variables mListaFiltrosConGrupos y mListaFiltrosFacetasUsuarioConGrupos almacenamos tal cual lo que venía

            mListaFiltrosConGrupos = new Dictionary<string, List<string>>(mListaFiltros);

            string separadorFacetaPorTipo = ";";
            List<string> listaClaves = new List<string>();
            foreach (string clave in mListaFiltros.Keys)
            {
                listaClaves.Add(clave);
            }
            //Eliminamos los prefijos de los tipos (que no sean el de por defecto)
            foreach (string clave in listaClaves)
            {
                if (clave.Contains(separadorFacetaPorTipo))
                {
                    List<string> valores = mListaFiltros[clave];

                    if (clave == "default;rdf:type")
                    {
                        if (mListaFiltros.ContainsKey("rdf:type"))
                        {
                            mListaFiltros["rdf:type"] = mListaFiltros["default;rdf:type"];
                        }
                        else
                        {
                            mListaFiltros.Add("rdf:type", mListaFiltros["default;rdf:type"]);
                        }
                    }
                    else
                    {
                        //Desechamos esos tipos
                        mListaFiltros.Remove(clave);
                    }

                    if (mListaFiltros.ContainsKey("default;rdf:type"))
                    {
                        //Si se filtra por ese tipo no se desecha
                        string filtroActual = mListaFiltros["default;rdf:type"][0];

                        if (filtroActual == clave.Substring(0, clave.IndexOf(separadorFacetaPorTipo)))
                        {
                            string nuevaClave = clave.Substring(clave.IndexOf(separadorFacetaPorTipo) + 1);

                            if (mListaFiltros.ContainsKey(nuevaClave))
                            {
                                mListaFiltros[nuevaClave].AddRange(valores);
                            }
                            else
                            {
                                mListaFiltros.Add(nuevaClave, valores);
                            }
                        }
                    }
                }
            }

            mListaFiltros.Remove("default;rdf:type");

            mListaFiltrosFacetasUsuarioConGrupos = new Dictionary<string, List<string>>(mListaFiltrosFacetasUsuario);

            List<string> listaClavesFacetasUsuarios = new List<string>();
            foreach (string clave in mListaFiltrosFacetasUsuario.Keys)
            {
                listaClavesFacetasUsuarios.Add(clave);
            }

            //Eliminamos los prefijos de los tipos (que no sean el de por defecto)
            foreach (string clave in listaClavesFacetasUsuarios)
            {
                if (clave.Contains(separadorFacetaPorTipo))
                {
                    List<string> valores = mListaFiltrosFacetasUsuario[clave];

                    if (clave == "default;rdf:type")
                    {
                        if (mListaFiltros.ContainsKey("rdf:type"))
                        {
                            mListaFiltrosFacetasUsuario["rdf:type"] = mListaFiltrosFacetasUsuario["default;rdf:type"];
                        }
                        else
                        {
                            mListaFiltrosFacetasUsuario.Add("rdf:type", mListaFiltrosFacetasUsuario["default;rdf:type"]);
                        }
                    }
                    else
                    {
                        mListaFiltrosFacetasUsuario.Remove(clave);
                    }

                    if (mListaFiltrosFacetasUsuario.ContainsKey("default;rdf:type"))
                    {
                        string filtroActual = mListaFiltrosFacetasUsuario["default;rdf:type"][0];

                        if (filtroActual == clave.Substring(0, clave.IndexOf(separadorFacetaPorTipo)))
                        {
                            string nuevaClave = clave.Substring(clave.IndexOf(separadorFacetaPorTipo) + 1);

                            if (mListaFiltrosFacetasUsuario.ContainsKey(nuevaClave))
                            {
                                mListaFiltrosFacetasUsuario[nuevaClave].AddRange(valores);
                            }
                            else
                            {
                                mListaFiltrosFacetasUsuario.Add(nuevaClave, valores);
                            }
                        }
                    }
                }
            }
            mListaFiltrosFacetasUsuario.Remove("default;rdf:type");
        }

        #endregion

        #region Propiedades

        public List<string> FacetasComunesGrupos
        {
            get
            {
                List<string> listaFacetasComunesGrupos = new List<string>();
                if (GruposPorTipo)
                {
                    //facetascomunes=harmonise:region|facetascomunes=harmonise:region
                    Dictionary<string, string> listaPropiedades = UtilCadenas.ObtenerPropiedadesDeTexto(FilaPestanyaBusquedaActual.GruposConfiguracion);
                    if (listaPropiedades.ContainsKey("facetascomunes"))
                    {
                        string[] facetascomunes = listaPropiedades["facetascomunes"].Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string facetacomun in facetascomunes)
                        {
                            if (!listaFacetasComunesGrupos.Contains(facetacomun))
                            {
                                listaFacetasComunesGrupos.Add(facetacomun);
                            }
                        }
                    }
                }
                return listaFacetasComunesGrupos;
            }
        }

        public Dictionary<string, List<string>> GruposAgrupados
        {
            get
            {
                Dictionary<string, List<string>> listaGrupos = new Dictionary<string, List<string>>();
                if (GruposPorTipo)
                {
                    //winery&Comer y beber|gastro&Comer y beber|destination&Ver y hacer|attraction&Ver y hacer|rtroute&Ver y hacer|events&Ver y hacer|accommodation&Dormir
                    Dictionary<string, string> listaPropiedades = UtilCadenas.ObtenerPropiedadesDeTexto(FilaPestanyaBusquedaActual.GruposConfiguracion);
                    if (listaPropiedades.ContainsKey("grupos"))
                    {
                        string[] pares = listaPropiedades["grupos"].Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string par in pares)
                        {
                            string tipo = par.Split(new string[] { "&" }, StringSplitOptions.RemoveEmptyEntries)[0];
                            string nombre = par.Split(new string[] { "&" }, StringSplitOptions.RemoveEmptyEntries)[1];
                            if (listaGrupos.ContainsKey(nombre))
                            {
                                listaGrupos[nombre].Add(tipo);
                            }
                            else
                            {
                                List<string> nuevaLista = new List<string>();
                                nuevaLista.Add(tipo);
                                listaGrupos.Add(nombre, nuevaLista);
                            }
                        }
                    }
                }
                return listaGrupos;
            }
        }

        /// <summary>
        /// Devuelve si las facetas tienen que mostrarse como grupos de tipos
        /// </summary>
        public bool GruposPorTipo
        {
            get
            {
                return FilaPestanyaBusquedaActual != null && FilaPestanyaBusquedaActual.GruposPorTipo;
            }
        }

        /// <summary>
        /// Devuelve la pestaña en la que estamos
        /// </summary>
        public Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.ProyectoPestanyaBusqueda FilaPestanyaBusquedaActual
        {
            get
            {
                if (mPestanyaActualID != Guid.Empty)
                {
                    foreach (Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.ProyectoPestanyaBusqueda filaPestanya in PestanyasProyectoDW.ListaProyectoPestanyaBusqueda)
                    {
                        if (filaPestanya.PestanyaID == mPestanyaActualID)
                        {
                            return filaPestanya;
                        }
                    }
                }
                return null;
            }
        }

        public GestionFacetas GestorFacetas
        {
            get
            {
                if (mGestorFacetas == null)
                {
                    CargarGestorFacetas();
                }
                return this.mGestorFacetas;
            }
        }

        private GestionFacetas mGestorFacetasOriginal;

        public GestionFacetas GestorFacetasOriginal
        {
            get
            {
                if (mGestorFacetasOriginal == null)
                {
                    CargarGestorFacetas();
                }
                return mGestorFacetasOriginal;
            }
            set
            {
                mGestorFacetasOriginal = value;
            }
        }

        /// <summary>
        /// Obtiene la identidad del usuario actual
        /// </summary>
        public Identidad IdentidadActual
        {
            get
            {
                return mIdentidadActual;
            }
        }

        #region Idiomas

        /// <summary>
        /// Obtiene o establece la información sobre el idioma del usuario
        /// </summary>
        public UtilIdiomas UtilIdiomas
        {
            get
            {
                if (mUtilIdiomas == null)
                {
                    if (ProyectoSeleccionado != null)
                    {
                        mUtilIdiomas = new UtilIdiomas(mLanguageCode, ProyectoSeleccionado.Clave, ProyectoSeleccionado.PersonalizacionID, PersonalizacionEcosistemaID, mLoggingService, mEntityContext, mConfigService);
                    }
                    else
                    {
                        mUtilIdiomas = new UtilIdiomas(mLanguageCode, mLoggingService, mEntityContext, mConfigService);
                    }
                }
                return mUtilIdiomas;
            }
            set
            {
                mUtilIdiomas = value;
            }
        }

        /// <summary>
        /// Obtiene la URL del los elementos de contenido de la página
        /// </summary>
        public string BaseURLContent
        {
            get
            {
                return mControladorBase.BaseURLContent;
            }
        }

        /// <summary>
        /// Obtiene el texto "Sin especifiar" para el idioma concreto
        /// </summary>
        private string TextoSinEspecificar
        {
            get
            {
                return GetText("CONFIGURACIONFACETADO", "SINESPECIFICAR");
            }
        }

        #endregion

        #region Gestores y DataSets del proyecto

        /// <summary>
        /// Obtiene el gestor de tesauro del proyecto actual
        /// </summary>
        public GestionTesauro GestorTesauro
        {
            get
            {
                if (mGestorTesauro == null)
                {
                    if (mTipoBusqueda == TipoBusqueda.EditarRecursosPerfil)
                    {

                        string idOrganizacionOUsuario = mGrafoID.Replace("perfil/", "");
                        OrganizacionCN organizacionCN = new OrganizacionCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                        bool esorganizacion = organizacionCN.ExisteOrganizacionPorOrganizacionID(idOrganizacionOUsuario);
                        organizacionCN.Dispose();

                        if (esorganizacion)
                        {
                            mGestorTesauro = new GestionTesauro(new TesauroCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication).ObtenerTesauroOrganizacion(new Guid(idOrganizacionOUsuario)), mLoggingService, mEntityContext);
                        }
                        else
                        {
                            //Adaptación Facetas Viejas a Facetas Nuevas.
                            IdentidadCN identCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            Guid personaID = identCN.ObtenerPersonaIDDeIdentidad(mIdentidadID).Value;
                            identCN.Dispose();

                            mGestorTesauro = new GestionTesauro(new TesauroCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication).ObtenerTesauroUsuarioPorPersonaID(personaID), mLoggingService, mEntityContext);
                        }
                    }
                    else if (mTipoBusqueda == TipoBusqueda.VerRecursosPerfil)
                    {
                        Guid PerfilID = new Guid(mGrafoID.Substring(mGrafoID.IndexOf("/") + 1));

                        TesauroCN tesauroCN = new TesauroCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                        try
                        {
                            IdentidadCN idenCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            Es.Riam.Gnoss.AD.EntityModel.Models.IdentidadDS.Perfil perfil = idenCN.ObtenerFilaPerfilPorID(PerfilID);
                            idenCN.Dispose();

                            Guid personaID = Guid.Empty;
                            if (perfil != null && perfil.PersonaID.HasValue)
                            {
                                personaID = perfil.PersonaID.Value;
                            }
                            mGestorTesauro = new GestionTesauro(tesauroCN.ObtenerTesauroUsuarioPorPersonaID(personaID), mLoggingService, mEntityContext);
                        }
                        catch (Exception)
                        {
                            mGestorTesauro = new GestionTesauro(tesauroCN.ObtenerTesauroOrganizacion(PerfilID), mLoggingService, mEntityContext);
                        }
                        tesauroCN.Dispose();
                    }
                    else if (mProyectoOrigenID != Guid.Empty)
                    {
                        mGestorTesauro = new GestionTesauro(new TesauroCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication).ObtenerTesauroDeProyecto(mProyectoOrigenID), mLoggingService, mEntityContext);
                    }
                    else
                    {
                        mGestorTesauro = new GestionTesauro(new TesauroCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication).ObtenerTesauroDeProyecto(mProyectoID), mLoggingService, mEntityContext);
                    }
                }
                return mGestorTesauro;
            }
        }

        /// <summary>
        /// Obtiene los niveles de cerificación del proyecto actual
        /// </summary>
        public DataWrapperProyecto NivelesCertificacionDW
        {
            get
            {
                if (mNivelesCertificacionDW == null)
                {
                    if (!mProyectoID.Equals(ProyectoAD.MetaProyecto))
                    {
                        //Obtengo los niveles de certificación de la caché
                        ProyectoCL proyCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                        mNivelesCertificacionDW = proyCL.ObtenerNivelesCertificacionRecursosProyecto(mProyectoID);
                    }
                    else
                    {
                        //Creo el dataSet sin niveles de certficación
                        mNivelesCertificacionDW = new DataWrapperProyecto();
                    }
                }
                return mNivelesCertificacionDW;
            }
        }

        /// <summary>
        /// Obtiene la fila de parámetros generales del proyecto
        /// </summary>
        //public ParametroGeneralDS.ParametroGeneralRow ParametrosGenerales
        public ParametroGeneral ParametrosGenerales
        {
            get
            {
                if (mParametrosGenerales == null)
                {
                    GestorParametroGeneral gestorParametroGeneral = new GestorParametroGeneral();
                    ParametroGeneralCL paramCL = new ParametroGeneralCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
                    gestorParametroGeneral = paramCL.ObtenerParametrosGeneralesDeProyecto(mProyectoID);
                    paramCL.Dispose();

                    ParametroGeneral parametroGeneral = gestorParametroGeneral.ListaParametroGeneral.Find(paramGeneral => paramGeneral.ProyectoID.Equals(mProyectoID));
                    if (parametroGeneral != null)
                    {
                        mParametrosGenerales = parametroGeneral;
                    }
                }
                return mParametrosGenerales;
            }
        }

        /// <summary>
        /// Parámetros de un proyecto.
        /// </summary>
        public Dictionary<string, string> ParametroProyecto
        {
            get
            {
                if (mParametroProyecto == null)
                {
                    ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    mParametroProyecto = proyectoCL.ObtenerParametrosProyecto(ProyectoSeleccionado.Clave);
                    proyectoCL.Dispose();
                }

                return mParametroProyecto;
            }
        }

        private bool CachearFacetas
        {
            get
            {
                return !(ParametroProyecto.ContainsKey("CacheFacetas") && ParametroProyecto["CacheFacetas"].Equals("0"));
            }
        }

        /// <summary>
        /// Obtiene la fila de parámetros generales del proyecto
        /// </summary>
        public Proyecto ProyectoSeleccionado
        {
            get
            {
                if (mProyectoSeleccionado == null)
                {
                    ProyectoCL proyectoCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    GestionProyecto gestorProyecto = new GestionProyecto(proyectoCL.ObtenerProyectoPorID(mProyectoID), mLoggingService, mEntityContext);

                    if (gestorProyecto.ListaProyectos.Count > 0 && gestorProyecto.ListaProyectos.ContainsKey(mProyectoID))
                    {
                        mProyectoSeleccionado = gestorProyecto.ListaProyectos[mProyectoID];
                    }
                }
                return mProyectoSeleccionado;
            }
        }

        /// <summary>
        /// Obtiene la URL base de la página
        /// </summary>
        public string BaseURL
        {
            get
            {
                return mControladorBase.BaseURL;
            }
        }

        /// <summary>
        /// Obtiene la URL del los elementos de contenido de la página
        /// </summary>
        public string BaseURLPersonalizacion
        {
            get
            {
                return mControladorBase.BaseURLPersonalizacion;
            }
        }

        /// <summary>
        /// Obtiene la URL base en el idioma correspondiente
        /// </summary>
        public string BaseURLIdioma
        {
            get
            {
                string baseUrlIdioma = BaseURL;
                if (!string.IsNullOrEmpty(ParametrosGenerales.IdiomaDefecto) && UtilIdiomas.LanguageCode != ParametrosGenerales.IdiomaDefecto)
                {
                    baseUrlIdioma += $"/{UtilIdiomas.LanguageCode}";
                }
                return baseUrlIdioma;
            }
        }


        /// <summary>
        /// Obtiene la URL del los elementos estaticos de la página
        /// </summary>
        public string BaseURLStatic
        {
            get
            {
                string urlStatic = mConfigService.ObtenerUrlServicio("urlStatic");
                return urlStatic;
            }
        }

        /// <summary>
        /// Lista de ontologías que tiene este proyecto
        /// </summary>
        private Dictionary<string, List<string>> InformacionOntologias
        {
            get
            {
                if (mInformacionOntologias == null)
                {
                    mInformacionOntologias = mUtilServiciosFacetas.ObtenerInformacionOntologias(ProyectoSeleccionado.FilaProyecto.OrganizacionID, ProyectoSeleccionado.Clave);
                }
                return mInformacionOntologias;
            }
        }

        /// <summary>
        /// Fila de proyecto.
        /// </summary>
        public Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.Proyecto FilaProyecto
        {
            get
            {
                if (mFilaProyecto == null)
                {
                    ProyectoCL proyCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    mFilaProyecto = proyCL.ObtenerFilaProyecto(mProyectoID);
                    proyCL.Dispose();
                }

                return mFilaProyecto;
            }
        }

        /// <summary>
        /// Obtiene las pestañas del proyecto y su configuracion
        /// </summary>
        public DataWrapperProyecto PestanyasProyectoDW
        {
            get
            {
                if (mPestanyasProyectoDW == null)
                {
                    ProyectoCL proyCL = new ProyectoCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mVirtuosoAD, mServicesUtilVirtuosoAndReplication);
                    mPestanyasProyectoDW = proyCL.ObtenerPestanyasProyecto(mProyectoID);
                }
                return mPestanyasProyectoDW;
            }
        }


        /// <summary>
        /// Obtiene el dataset de parámetros de aplicación
        /// </summary>
        public List<ParametroAplicacion> ParametrosAplicacionDS
        {
            get
            {
                if (mParametrosAplicacionDS == null)
                {
                    ParametroAplicacionCL paramCL = new ParametroAplicacionCL(mEntityContext, mLoggingService, mRedisCacheWrapper, mConfigService, mServicesUtilVirtuosoAndReplication);
                    mParametrosAplicacionDS = paramCL.ObtenerParametrosAplicacionPorContext();
                }
                return mParametrosAplicacionDS;
            }
        }


        /// <summary>
        /// Obtiene si se trata de un ecosistema sin metaproyecto
        /// </summary>
        public Guid PersonalizacionEcosistemaID
        {
            get
            {
                if (!mPersonalizacionEcosistemaID.HasValue)
                {
                    mPersonalizacionEcosistemaID = Guid.Empty;
                    List<ParametroAplicacion> parametrosAplicacion = ParametrosAplicacionDS.Where(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.PersonalizacionEcosistemaID.ToString())).ToList();
                    if (parametrosAplicacion.Count > 0)
                    {
                        mPersonalizacionEcosistemaID = new Guid(parametrosAplicacion.FirstOrDefault().Valor.ToString());
                    }
                }
                return mPersonalizacionEcosistemaID.Value;
            }
        }

        /// <summary>
        /// Obtiene si se trata de un ecosistema sin metaproyecto
        /// </summary>
        public bool ComunidadExcluidaPersonalizacionEcosistema
        {
            get
            {
                if (!mComunidadExcluidaPersonalizacionEcosistema.HasValue)
                {
                    mComunidadExcluidaPersonalizacionEcosistema = false;

                    List<ParametroAplicacion> parametrosAplicaicion = ParametrosAplicacionDS.Where(parametroApp => parametroApp.Parametro.Equals(TiposParametrosAplicacion.ComunidadesExcluidaPersonalizacion.ToString())).ToList();
                    if (parametrosAplicaicion.Count > 0)
                    {
                        List<string> listaComunidadesExcluidas = new List<string>(parametrosAplicaicion[0].Valor.ToString().ToUpper().Split(','));

                        mComunidadExcluidaPersonalizacionEcosistema = listaComunidadesExcluidas.Contains(ProyectoSeleccionado.Clave.ToString().ToUpper());
                    }
                }
                return mComunidadExcluidaPersonalizacionEcosistema.Value;
            }
        }

        #endregion

        /// <summary>
        /// Lista con las claves de facetas que son tesauro semántico y su datos en un dataSet.
        /// </summary>
        private Dictionary<string, FacetadoDS> TesauroSemDSFaceta
        {
            get
            {
                if (mTesauroSemDSFaceta == null)
                {
                    mTesauroSemDSFaceta = new Dictionary<string, FacetadoDS>();
                }

                return mTesauroSemDSFaceta;
            }
        }

        private void CargarFiltrosSearchPersonalizados()
        {
            if (mFiltrosSearchPersonalizados == null)
            {
                mFiltrosSearchPersonalizados = new Dictionary<string, Tuple<string, string, string, bool>>();

                if (ProyectoSeleccionado != null && ProyectoSeleccionado.GestorProyectos.DataWrapperProyectos.ListaProyectoSearchPersonalizado != null && ProyectoSeleccionado.GestorProyectos.DataWrapperProyectos.ListaProyectoSearchPersonalizado.Count > 0)
                {
                    foreach (Es.Riam.Gnoss.AD.EntityModel.Models.ProyectoDS.ProyectoSearchPersonalizado fila in ProyectoSeleccionado.GestorProyectos.DataWrapperProyectos.ListaProyectoSearchPersonalizado)
                    {

                        if (fila.WhereSPARQL.Equals("redis") && mListaFiltros.ContainsKey(fila.NombreFiltro))
                        {
                            string claveCache = mListaFiltros[fila.NombreFiltro].FirstOrDefault();
                            if (!string.IsNullOrEmpty(claveCache))
                            {
                                ConsultaCacheModelSerializable consultaCache = mGnossCache.ObtenerDeCache($"{mProyectoID}_{claveCache}", true) as ConsultaCacheModelSerializable;

                                if (consultaCache != null)
                                {
                                    Tuple<string, string, string, bool> tuplaAuxiliar = new Tuple<string, string, string, bool>(consultaCache.WhereSPARQL, consultaCache.OrderBy, consultaCache.WhereFacetasSPARQL, consultaCache.OmitirRdfType);
                                    mFiltrosSearchPersonalizados.Add(fila.NombreFiltro, tuplaAuxiliar);
                                }
                            }
                        }
                        else
                        {
                            string whereFacetasSparql = fila.WhereFacetasSPARQL;
                            if (!string.IsNullOrEmpty(whereFacetasSparql))
                            {
                                whereFacetasSparql = whereFacetasSparql.Replace("[PARAMETROIDIOMAUSUARIO]", mLanguageCode);
                            }
                            string orderBySPARQL = fila.OrderBySPARQL;
                            if (!string.IsNullOrEmpty(orderBySPARQL))
                            {
                                orderBySPARQL = orderBySPARQL.Replace("[PARAMETROIDIOMAUSUARIO]", mLanguageCode);
                            }

                            mFiltrosSearchPersonalizados.Add(fila.NombreFiltro, new Tuple<string, string, string, bool>(fila.WhereSPARQL.Replace("[PARAMETROIDIOMAUSUARIO]", mLanguageCode), orderBySPARQL, whereFacetasSparql, fila.OmitirRdfType));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Diccionario con los filtros tipo 'search' personalizados
        /// la clave es el nombre del filtro y el valor es 'WhereSPARQL','OrderBySPARQL','WhereFacetasSPARQL','omitirRdfType'
        /// </summary>
        private Dictionary<string, Tuple<string, string, string, bool>> FiltrosSearchPersonalizados
        {
            get
            {
                if (mFiltrosSearchPersonalizados == null)
                {
                    lock (mBloqueoFiltrosSearchPersonalizados)
                    {
                        if (mFiltrosSearchPersonalizados == null)
                        {
                            CargarFiltrosSearchPersonalizados();
                        }
                    }
                }
                return mFiltrosSearchPersonalizados;
            }
        }
        #endregion
    }
}