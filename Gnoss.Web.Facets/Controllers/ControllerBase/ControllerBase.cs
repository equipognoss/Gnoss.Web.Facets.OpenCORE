using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.Usuarios;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Logica.Identidad;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Util;
using Es.Riam.Web.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using static Es.Riam.Web.Util.UtilCookies;

namespace ServicioCargaFacetas
{
    public class ControllerBase : Controller
    {
        protected EntityContext mEntityContext;
        protected LoggingService mLoggingService;
        protected VirtuosoAD mVirtuosoAD;
        protected ConfigService mConfigService;
        protected RedisCacheWrapper mRedisCacheWrapper;
        protected GnossCache mGnossCache;
        protected IHttpContextAccessor mHttpContextAccessor;
        protected UtilWeb mUtilWeb;
        protected IServicesUtilVirtuosoAndReplication mServicesUtilVirtuosoAndReplication;

        public ControllerBase(LoggingService loggingService, ConfigService configService, EntityContext entityContext, RedisCacheWrapper redisCacheWrapper, GnossCache gnossCache, VirtuosoAD virtuosoAD, IHttpContextAccessor httpContextAccessor, IServicesUtilVirtuosoAndReplication servicesUtilVirtuosoAndReplication)
        {
            mLoggingService = loggingService;
            mVirtuosoAD = virtuosoAD;
            mConfigService = configService;
            mEntityContext = entityContext;
            mRedisCacheWrapper = redisCacheWrapper;
            mGnossCache = gnossCache;
            mHttpContextAccessor = httpContextAccessor;
            mUtilWeb = new UtilWeb(mHttpContextAccessor);
            mServicesUtilVirtuosoAndReplication = servicesUtilVirtuosoAndReplication;
        }

        [NonAction]
        private bool TienePersonalizacion()
        {
            bool tienePersonalizacion = false;

            if ((!string.IsNullOrEmpty((string)ViewBag.Personalizacion) || !string.IsNullOrEmpty((string)ViewBag.PersonalizacionEcosistema)) && ViewBag.Comunidad != null)
            {
                tienePersonalizacion = true;
            }

            return tienePersonalizacion;
        }
        [NonAction]
        protected internal PartialViewResult PartialView(string viewName)
        {
            if (TienePersonalizacion())
            {
                string nombreVista = viewName;
                nombreVista = nombreVista.Replace("../Shared", "").Trim('/');

                List<string> listaPersonalizaciones = ViewBag.Comunidad.ListaPersonalizaciones;
                List<string> listaPersonalizacionesEcosistema = ViewBag.Comunidad.ListaPersonalizacionesEcosistema;

                if (listaPersonalizaciones.Contains("/Views/" + (string)ViewBag.ControllerName + "/" + nombreVista + ".cshtml") || listaPersonalizaciones.Contains("/Views/" + "Shared" + "/" + nombreVista + ".cshtml"))
                {
                    return base.PartialView(viewName + (string)ViewBag.Personalizacion);
                }
                else if (listaPersonalizacionesEcosistema.Contains("/Views/" + (string)ViewBag.ControllerName + "/" + nombreVista + ".cshtml") || listaPersonalizacionesEcosistema.Contains("/Views/" + "Shared" + "/" + nombreVista + ".cshtml"))
                {
                    return base.PartialView(viewName + (string)ViewBag.PersonalizacionEcosistema);
                }
            }
            return base.PartialView(viewName);
        }
        [NonAction]
        protected internal virtual PartialViewResult PartialView(string viewName, object model)
        {
            if (TienePersonalizacion())
            {
                string nombreVista = viewName;
                nombreVista = nombreVista.Replace("../Shared", "").Trim('/');

                List<string> listaPersonalizaciones = ViewBag.Comunidad.ListaPersonalizaciones;
                List<string> listaPersonalizacionesEcosistema = ViewBag.Comunidad.ListaPersonalizacionesEcosistema;

                if (listaPersonalizaciones.Contains("/Views/" + (string)ViewBag.ControllerName + "/" + nombreVista + ".cshtml") || listaPersonalizaciones.Contains("/Views/" + "Shared" + "/" + nombreVista + ".cshtml"))
                {
                    return base.PartialView(viewName + (string)ViewBag.Personalizacion, model);
                }
                else if (listaPersonalizacionesEcosistema.Contains("/Views/" + (string)ViewBag.ControllerName + "/" + nombreVista + ".cshtml") || listaPersonalizacionesEcosistema.Contains("/Views/" + "Shared" + "/" + nombreVista + ".cshtml"))
                {
                    return base.PartialView(viewName + (string)ViewBag.PersonalizacionEcosistema, model);
                }
            }
            return base.PartialView(viewName, model);
        }
        [NonAction]
        protected internal ViewResult View(string viewName)
        {
            return base.View(ObtenerNombreVista(viewName));
        }
        [NonAction]
        public string ObtenerNombreVista(string viewName)
        {
            if (TienePersonalizacion())
            {
                List<string> listaPersonalizaciones = ViewBag.Comunidad.ListaPersonalizaciones;
                List<string> listaPersonalizacionesEcosistema = ViewBag.Comunidad.ListaPersonalizacionesEcosistema;

                if (listaPersonalizaciones.Contains("/Views/" + (string)ViewBag.ControllerName + "/" + viewName + ".cshtml"))
                {
                    return viewName + (string)ViewBag.Personalizacion;
                }
                else if (listaPersonalizacionesEcosistema.Contains("/Views/" + (string)ViewBag.ControllerName + "/" + viewName + ".cshtml"))
                {
                    return viewName + (string)ViewBag.PersonalizacionEcosistema;
                }
            }
            return viewName;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
#if !DEBUG
            if (mConfigService.PeticionHttps() && !((Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)filterContext.ActionDescriptor).ActionName.Contains("Cache"))
            {
                Guid identidadID = Guid.Empty;
                Guid.TryParse(Request.Form["pIdentidadID"], out identidadID);

                if (identidadID.Equals(Guid.Empty))
                {
                    Guid.TryParse(Request.Form["identidad"], out identidadID);

                    if (identidadID.Equals(Guid.Empty))
                    {
                        identidadID = UsuarioAD.Invitado;
                    }
                }

                if (!identidadID.Equals(UsuarioAD.Invitado))
                {
                    try
                    {
                        Dictionary<string, string> cookie = UtilCookies.FromLegacyCookieString(Request.Cookies["_UsuarioActual"], mEntityContext);
                        if (cookie != null && cookie.Count != 0)
                        {

                            Guid usuarioID = new Guid(cookie["usuarioID"]);
                            IdentidadCN identidadCN = new IdentidadCN(mEntityContext, mLoggingService, mConfigService, mServicesUtilVirtuosoAndReplication);
                            Guid usuarioIDDeBD = identidadCN.ObtenerUsuarioIDConIdentidadID(identidadID);
                            if (!usuarioIDDeBD.Equals(usuarioID))
                            {
                                filterContext.Result = new UnauthorizedResult();
                            }
                        }
                        else
                        {
                            string identidadIDCadena = Request.Headers["Authorization"];
                            if (string.IsNullOrEmpty(identidadIDCadena))
                            {
                                filterContext.Result = new UnauthorizedResult();
                            }
                            else
                            {
                                if(!identidadID.Equals(Guid.Parse(identidadIDCadena.Split("bearer ")[1])))
                                {
                                    filterContext.Result = new UnauthorizedResult();
                                }
                            }
                        }
                    }
                    catch (InvalidCypherTextException)
                    {
                        if (mHttpContextAccessor.HttpContext.Request.Cookies.ContainsKey("_UsuarioActual"))
                        {
                            Response.Cookies.Append("_UsuarioActual", Request.Cookies["_UsuarioActual"], new CookieOptions { Expires = new DateTime(2000, 1, 1) });
                        }
                        filterContext.Result = new UnauthorizedResult();
                    }
                    catch (Exception)
                    {
                        filterContext.Result = new UnauthorizedResult();
                    }

                }
            }
#endif
        }
    }
}
