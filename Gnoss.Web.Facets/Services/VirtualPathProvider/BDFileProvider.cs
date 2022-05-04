using Es.Riam.Gnoss.Util.General;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gnoss.Web.Services.VirtualPathProvider
{
    public class BDFileProvider : IFileProvider
    {
        readonly private BDVirtualPath _bdVirtualPath;
        LoggingService _loggingService;
        public BDFileProvider(LoggingService loggingService,BDVirtualPath bdVirtualPath)
        {
            _loggingService = loggingService;
            _bdVirtualPath = bdVirtualPath;
        }
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return null;
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            var result = new BDFileInfo(_loggingService, _bdVirtualPath, subpath);
            return result.Exists ? result as IFileInfo : new NotFoundFileInfo(subpath);
        }

        public IChangeToken Watch(string filter)
        {
            var bdChangeToken = new BDChangeToken(filter);
            return bdChangeToken;
        }
    }
}
