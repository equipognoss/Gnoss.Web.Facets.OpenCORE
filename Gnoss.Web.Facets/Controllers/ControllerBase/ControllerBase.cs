using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ServicioCargaFacetas
{
    public class ControllerBase : Controller
    {
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



    }
}
