using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gnoss.Web.Services.VirtualPathProvider
{
    /// <summary>
    /// Comprueba si la página ha sido cambiada para reemplazarla por la versión de caché
    /// </summary>
    public class BDChangeToken : IChangeToken
    {
        readonly private string _viewPath;
        public BDChangeToken(string viewPath)
        {
            _viewPath = viewPath;

        }
        public bool ActiveChangeCallbacks => false;

        public bool HasChanged
        {
            get
            {
                if (_viewPath.Contains("$$$") && !BDVirtualPath.ListaRutasVirtuales.ContainsKey(_viewPath))
                {
                    return true;
                }
                return false;
            }
        }

        public IDisposable RegisterChangeCallback(Action<object> callback, object state)
        {
            return EmptyDisposable.Instance;
        }
    }

    internal class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new EmptyDisposable();
        private EmptyDisposable() { }
        public void Dispose()
        {
            // No hace nada.
        }
    }
}
