using Es.Riam.Gnoss.Util.General;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gnoss.Web.Services.VirtualPathProvider
{
    /// <summary>
    /// Clase para obtener un fichero de Base de datos mediante su ruta virtual
    /// </summary>
    public class BDFileInfo : IFileInfo
    {
        readonly private string _viewPath;
        readonly private BDVirtualPath _bdVirtualPath;
        readonly private LoggingService _loggingService;
        private bool _exists;
        private byte[] _viewContent;
        public BDFileInfo(LoggingService loggingService, BDVirtualPath bdVirtualPath, string viewPath)
        {
            _viewPath = viewPath;
            _loggingService = loggingService;
            _bdVirtualPath = bdVirtualPath;
            GetView(viewPath);
        }

        public bool Exists => _exists;

        public bool IsDirectory => false;

        public DateTimeOffset LastModified => throw new NotImplementedException();

        /// <summary>
        /// El tamaño del fichero en bytes, -1 si es un directorio o no existe el fichero
        /// </summary>
        public long Length
        {
            get
            {
                using (var stream = new MemoryStream(_viewContent))
                {
                    return stream.Length;
                }
            }
        }

        /// <summary>
        /// El nombre del fichero
        /// </summary>
        public string Name => Path.GetFileName(_viewPath);

        public string PhysicalPath => null;

        /// <summary>
        /// Devuelve el fichero como un stream de lectura. Se debería cerrar el stream una vez leído
        /// </summary>
        /// <returns>El stream del fichero</returns>
        public Stream CreateReadStream()
        {
            return new MemoryStream(_viewContent);
        }

        /// <summary>
        /// Obtiene la información del fichero
        /// </summary>
        /// <param name="viewPath">ruta virtual del fichero</param>
        private void GetView(string viewPath)
        {
            _loggingService.AgregarEntrada($"GetFile {viewPath}");

            string virtualPathAux = GetRealPath(viewPath);

            string data = string.Empty;
            if (virtualPathAux.Contains("$$$"))
            {
                data = _bdVirtualPath.FindPage(virtualPathAux);
                if (!string.IsNullOrEmpty(data))
                {
                    _exists = true;
                    _viewContent = Encoding.UTF8.GetBytes(data);
                }
            }
            _loggingService.AgregarEntrada($"Fin GetFile");
        }

        private string GetRealPath(string virtualPath)
        {
            string virtualPathAux = virtualPath.Trim('~');
            string directorioVistas = "";
            if (virtualPathAux.Contains("/Views/"))
            {
                //if (virtualPathAux.Contains("$$$"))
                //{ 
                    directorioVistas = "Views";
                //}
                virtualPathAux = virtualPathAux.Substring(virtualPathAux.IndexOf($"/{directorioVistas}/"));

                if (virtualPathAux.Contains("/../"))
                {
                    virtualPathAux = $"/{directorioVistas}/" + virtualPathAux.Substring(virtualPathAux.IndexOf("/../") + 4);
                }
            }
            return virtualPathAux;
        }
    }
}
