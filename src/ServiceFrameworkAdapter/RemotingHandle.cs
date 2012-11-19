using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using C_NSF = CodeSharp.ServiceFramework;
using T_NSF = Taobao.ServiceFramework;

namespace ServiceFrameworkAdapter
{
    /// <summary>Code#'s to Taobao's Adapter
    /// </summary>
    public class RemotingHandle : C_NSF.Interfaces.IRemoteHandle
    {
        private ConcurrentDictionary<string, Type> _uriTypes;
        private C_NSF.Interfaces.ILog _log;
        private C_NSF.Remoting.RemotingHandle _defaultHandle;
        public RemotingHandle(C_NSF.Endpoint endpoint)
        {
            var factory = endpoint.Resolve<C_NSF.Interfaces.ILoggerFactory>();
            this._log = factory.Create(typeof(RemotingHandle));
            this._defaultHandle = new C_NSF.Remoting.RemotingHandle(factory);
            this._uriTypes = new ConcurrentDictionary<string, Type>();
        }

        public void Expose(Uri uri)
        {
            this._defaultHandle.Expose(uri);
        }
        public bool TryConnect(Uri uri, int? timeout, out Exception e)
        {
            if (timeout.HasValue)
                return this.TryConnectTimeout(uri, timeout.Value, out e);

            try
            {
                var v = this.GetVersion(uri);
                e = null;
                return true;
            }
            catch (Exception ex)
            {
                e = ex;
                this._log.WarnFormat("连接到NSF服务节点{0}发生异常", uri, e);
                return false;
            }
        }
        public string GetVersion(Uri uri)
        {
            var f = this.GetFacade(uri);
            return this.Is_C_NSF(f) 
                ? (f as C_NSF.Remoting.RemoteFacade).GetVersion()
                : (f as T_NSF.Remoting.RemoteFacade).GetVersion();
        }
        public C_NSF.ServiceConfigTable GetServiceConfigs(Uri uri)
        {
            var f = this.GetFacade(uri);

            if (this.Is_C_NSF(f))
                return (f as C_NSF.Remoting.RemoteFacade).GetServiceConfigs();

            var result2 = (f as T_NSF.Remoting.RemoteFacade).GetServiceConfigs();
            return new C_NSF.ServiceConfigTable()
            {
                Version = result2.Version,
                HostUri = result2.HostUri,
                Configs = result2.Configs.Select(o => this.Parse(o)).ToArray(),
                Services = result2.Services.Select(o => new C_NSF.ServiceInfo()
                {
                    AssemblyName = o.AssemblyName,
                    LoadBalancingAlgorithm = o.LoadBalancingAlgorithm,
                    Name = o.Name,
                    Configs = o.Configs.Select(p => this.Parse(p)).ToArray()
                }).ToArray()

            };
        }
        public string GetServiceDescription(C_NSF.ServiceConfig service)
        {
            var f = this.GetFacade(service.HostUri);

            return this.Is_C_NSF(f)
                ? (f as C_NSF.Remoting.RemoteFacade).GetServiceDescription(service)
                : (f as T_NSF.Remoting.RemoteFacade).GetServiceDescription(this.Parse(service));
        }
        public string Invoke(C_NSF.ServiceCall call)
        {
            var f = this.GetFacade(call.Target.HostUri);

            return this.Is_C_NSF(f)
                ? (f as C_NSF.Remoting.RemoteFacade).Invoke(call)
                : (f as T_NSF.Remoting.RemoteFacade).Invoke(this.Parse(call));
        }
        public void Register(Uri uri, C_NSF.ServiceConfig[] services)
        {
            var f = this.GetFacade(uri);

            if (this.Is_C_NSF(f))
                (f as C_NSF.Remoting.RemoteFacade).Register(services);
            else
                (f as T_NSF.Remoting.RemoteFacade).Register(services.Select(o => this.Parse(o)).ToArray());
        }
        public void SendAnAsyncCall(Uri uri, C_NSF.ServiceCall call)
        {
            var f = this.GetFacade(uri);

            if (this.Is_C_NSF(f))
                (f as C_NSF.Remoting.RemoteFacade).InvokeAsync(call);
            else
                (f as T_NSF.Remoting.RemoteFacade).InvokeAsync(this.Parse(call));
        }

        private object GetFacade(Uri uri)
        {
            return this.GetFacade(uri.ToString());
        }
        private object GetFacade(string uri)
        {
            Type type;
            if (this._uriTypes.TryGetValue(uri, out type))
                if (type == typeof(C_NSF.Remoting.RemoteFacade))
                    return RemotingServices.Connect(type, uri) as C_NSF.Remoting.RemoteFacade;
                else
                    return RemotingServices.Connect(type, uri) as T_NSF.Remoting.RemoteFacade;

            C_NSF.Remoting.RemoteFacade f = null;

            #region C_NSF
            try
            {
                f = RemotingServices.Connect(typeof(C_NSF.Remoting.RemoteFacade), uri) as C_NSF.Remoting.RemoteFacade;
                f.GetVersion();
                this._uriTypes.AddOrUpdate(uri, typeof(C_NSF.Remoting.RemoteFacade), (k, t) => typeof(C_NSF.Remoting.RemoteFacade));
                return f;
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("RemoteFacade"))
                    throw e;

                this.RemoveRemotingIdentity(uri);
                this._log.Debug("发现类型不匹配的远程类型", e);
            }
            #endregion

            this._log.DebugFormat("激活适配，使用类型{0}为远程类型", typeof(T_NSF.Remoting.RemoteFacade));

            this._uriTypes.AddOrUpdate(uri
                , typeof(T_NSF.Remoting.RemoteFacade)
                , (k, t) => typeof(T_NSF.Remoting.RemoteFacade));
            var facade = (T_NSF.Remoting.RemoteFacade)RemotingServices.Connect(typeof(T_NSF.Remoting.RemoteFacade)
                , uri);

            return facade;
        }
        private bool TryConnectTimeout(Uri uri, int timeout, out Exception e)
        {
            //HACK:remoting没有提供tcp/http通道的connectionTimeout支持...

            Exception error = null;
            var t = new System.Threading.Thread(() =>
            {
                try { this.GetVersion(uri); }
                catch (Exception ex) { error = ex; }
            });
            t.Start();

            if (t.Join(timeout))
                return (e = error) == null;
            else
            {
                e = new Exception(string.Format("连接到{0}时超时（{1}ms）", uri, timeout));
                return false;
            }
        }

        private bool Is_C_NSF(object f)
        {
            return f is C_NSF.Remoting.RemoteFacade;
        }
        private C_NSF.ServiceConfig Parse(T_NSF.ServiceConfig c)
        {
            return new C_NSF.ServiceConfig()
            {
                AssemblyName = c.AssemblyName,
                HostUri = c.HostUri,
                LoadBalancingAlgorithm = c.LoadBalancingAlgorithm,
                Name = c.Name
            };
        }
        private T_NSF.ServiceConfig Parse(C_NSF.ServiceConfig c)
        {
            return new T_NSF.ServiceConfig()
            {
                AssemblyName = c.AssemblyName,
                HostUri = c.HostUri,
                LoadBalancingAlgorithm = c.LoadBalancingAlgorithm,
                Name = c.Name
            };
        }
        private T_NSF.ServiceCall Parse(C_NSF.ServiceCall call)
        {
            return new T_NSF.ServiceCall()
            {
                TargetMethod = call.TargetMethod,
                Identity = new T_NSF.Identity() { AuthKey = call.Identity.AuthKey, Source = call.Identity.Source },
                Target = this.Parse(call.Target),
                ArgumentCollection = call.ArgumentCollection.Select(o => new T_NSF.ServiceCallArgument(o.Key, o.Value)).ToArray()
            };
        }
        private void RemoveRemotingIdentity(string uri)
        {
            //HACK:由于IdentityHolder是内部类无法合法操作，以下硬编码依赖于.net bcl实现
            var t = Type.GetType("System.Runtime.Remoting.IdentityHolder")
                .GetField("_URITable"
                , BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null) as Hashtable;
            t.Remove(uri);
        }
    }
}