using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Gnoss.Web.Services.VirtualPathProvider
{
    /// <summary>
    /// FileProvider singleton que expone las vistas de base de datos al RuntimeViewCompiler.
    /// Usa IHttpContextAccessor para resolver los servicios scoped (BDVirtualPath, LoggingService)
    /// en el contexto de cada petición, evitando el problema de consumir servicios scoped
    /// desde un singleton.
    /// Se añade a IWebHostEnvironment.ContentRootFileProvider en el Configure() del Startup,
    /// de modo que RuntimeCompilationFileProvider (usado por AddRazorRuntimeCompilation) lo incluya.
    /// </summary>
    public sealed class BDRequestScopedFileProvider : IFileProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BDRequestScopedFileProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            if (!subpath.Contains("$$$"))
                return new NotFoundFileInfo(subpath);

            var sp = _httpContextAccessor.HttpContext?.RequestServices;
            if (sp == null)
                return new NotFoundFileInfo(subpath);

            var loggingService = sp.GetRequiredService<LoggingService>();
            var bdVirtualPath = sp.GetRequiredService<BDVirtualPath>();
            return new BDFileInfo(loggingService, bdVirtualPath, subpath);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
            => NotFoundDirectoryContents.Singleton;

        public IChangeToken Watch(string filter)
            // BDChangeToken solo usa el diccionario estático ListaRutasVirtuales,
            // no necesita HttpContext, por lo que es seguro devolverlo desde cualquier hilo.
            => filter.Contains("$$$") ? new BDChangeToken(filter) : NullChangeToken.Singleton;
    }
}
