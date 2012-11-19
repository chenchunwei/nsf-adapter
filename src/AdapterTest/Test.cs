using System;
using System.Runtime.Remoting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CodeSharp.ServiceFramework.Castles;
using Castle.MicroKernel.Registration;
using C_NSF = CodeSharp.ServiceFramework;
using T_NSF = Taobao.ServiceFramework;
using System.Runtime.Remoting.Channels;

namespace AdapterTest
{
    [TestClass]
    public class Test
    {
        [TestMethod]
        public void ConnectRemoting()
        {
            var f = (T_NSF.Remoting.RemoteFacade)RemotingServices.Connect(typeof(T_NSF.Remoting.RemoteFacade)
                 , "tcp://taobao-dev-ntfe01:1234/remote.rem");
            Console.WriteLine(f.GetVersion());
        }
        [TestMethod]
        public void ConnectProblem()
        {
            Console.WriteLine(ChannelServices.RegisteredChannels.Length);

            var f1 = RemotingServices.Connect(typeof(C_NSF.Remoting.RemoteFacade)
                 , "tcp://taobao-dev-ntfe01:1234/remote.rem") as C_NSF.Remoting.RemoteFacade;
            try { f1.GetVersion(); Assert.IsFalse(true); }
            catch (Exception e) { Console.WriteLine(e.Message); }

            Console.WriteLine(ChannelServices.RegisteredChannels.Length);

            //not work
            //ChannelServices.UnregisterChannel(ChannelServices.RegisteredChannels[0]);
            
            //connect twice on same uri
            var f2 = RemotingServices.Connect(typeof(T_NSF.Remoting.RemoteFacade)
                 , "tcp://taobao-dev-ntfe01:1234/remote.rem");
            Assert.IsNull(f2 as T_NSF.Remoting.RemoteFacade);
            //always first proxy type
            Assert.IsNotNull(f2 as C_NSF.Remoting.RemoteFacade);

            Console.WriteLine(ChannelServices.RegisteredChannels.Length);
        }
        [TestMethod]
        public void GetObject()
        {
            var f1 = Activator.GetObject(typeof(C_NSF.Remoting.RemoteFacade)
                 , "tcp://taobao-dev-ntfe01:1234/remote.rem", "1") as C_NSF.Remoting.RemoteFacade;
            try { f1.GetVersion(); Assert.IsFalse(true); }
            catch (Exception e) { Console.WriteLine(e.Message); }

            //GetObject twice on same uri
            var f2 = Activator.GetObject(typeof(T_NSF.Remoting.RemoteFacade)
                 , "tcp://taobao-dev-ntfe01:1234/remote.rem", "2");
            Assert.IsNull(f2 as T_NSF.Remoting.RemoteFacade);
            //always first proxy type
            Assert.IsNotNull(f2 as C_NSF.Remoting.RemoteFacade);
        }
        [TestMethod]
        public void RemoveIdentity()
        {
            ConnectProblem();

            System.Collections.Hashtable t = System.Type.GetType("System.Runtime.Remoting.IdentityHolder").GetField("_URITable"
                , System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null) as System.Collections.Hashtable;
            Console.WriteLine(t);
            foreach (var k in t.Keys)
                Console.WriteLine(k);

            /*
            var m = System.Type.GetType("System.Runtime.Remoting.IdentityHolder").GetMethod("RemoveIdentity"
                , System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                , null
                , new Type[] { typeof(String) }
                , new System.Reflection.ParameterModifier[] { new System.Reflection.ParameterModifier(1) });
            Console.WriteLine(m.Name);
            m.Invoke(null, new object[] { "tcp://taobao-dev-ntfe01:1234/remote.rem" });
            */

            //HACK:maybe have other problem, just for servicetype
            t.Remove("tcp://taobao-dev-ntfe01:1234/remote.rem");

            foreach (var k in t.Keys)
                Console.WriteLine(k);


            var f = Activator.GetObject(typeof(T_NSF.Remoting.RemoteFacade)
                 , "tcp://taobao-dev-ntfe01:1234/remote.rem", "2");
            Assert.IsNotNull(f as T_NSF.Remoting.RemoteFacade);
            //always first proxy type
            Assert.IsNull(f as C_NSF.Remoting.RemoteFacade);

            (f as T_NSF.Remoting.RemoteFacade).GetVersion();
        }

        [TestMethod]
        public void CodeSharp_to_Taobao_Adapter()
        {
            var c = new Castle.Windsor.WindsorContainer();
            c.Register(Component
                .For<C_NSF.Interfaces.IRemoteHandle>()
                .ImplementedBy<ServiceFrameworkAdapter.RemotingHandle>()
                .IsDefault());

            CodeSharp.ServiceFramework.Configuration
                .Configure()
                .Castle(c)
                .Associate(new Uri("tcp://taobao-dev-ntfe01:1234/remote.rem"))
                .Endpoint()
                .Run();

            var i = 0;
            while (i++ < 5)
                c.Resolve<Taobao.Facades.IUserService>().GetUserByDisplayName("侯昆");
        }
    }
}
