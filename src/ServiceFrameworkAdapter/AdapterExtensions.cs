using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.MicroKernel.Registration;

namespace CodeSharp.Framework.Castles
{
    //仅是封装范例，可取消以下实现以简化程序集依赖
    public static class AdapterExtensions
    {
        /// <summary>启用NSF对taobao版本的适配
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static SystemConfigWithCastle NSFAdapter(this SystemConfigWithCastle config)
        {
            config.Resolve(o =>
            {
                var c = o.Container;
                c.Register(Component
                    .For<CodeSharp.ServiceFramework.Interfaces.IRemoteHandle>()
                    .ImplementedBy<ServiceFrameworkAdapter.RemotingHandle>()
                    .IsDefault());
            });
            return config;
        }
    }
}