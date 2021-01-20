// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// 
    /// </summary>
    public static class MapActionEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static IEndpointConventionBuilder MapAction2<T, TResult>(
            this IEndpointRouteBuilder endpoints,
            Func<T, TResult> action)
        {
            var route = action.Method.GetCustomAttribute<RouteAttribute>();

            if (route is null)
            {
                throw new Exception();
            }

            return endpoints.Map(RoutePatternFactory.Parse(route.Template), async httpContext =>
            {
                var arg = await httpContext.Request.ReadFromJsonAsync<T>();

                if (arg is null)
                {
                    throw new Exception();
                }

                var result = action(arg);

                await httpContext.Response.WriteAsJsonAsync(result);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="services"></param>
        public static void AddMapActionServices(this IServiceCollection services)
        {
            services.AddSingleton<IApplicationModelProvider, MapActionModelProvider>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        private static void MapAction(
            this IEndpointRouteBuilder endpoints,
            Delegate action)
        {
            var modelProviders = endpoints.ServiceProvider.GetService<IEnumerable<IApplicationModelProvider>>();
            var modelProvider = modelProviders.OfType<MapActionModelProvider>().Single();
            modelProvider.Actions.Add(action);
        }

        #region Public MapAction overloads

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction(
            this IEndpointRouteBuilder endpoints,
            Action action)
        {
            endpoints.MapAction((Delegate)action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<T>(
            this IEndpointRouteBuilder endpoints,
            Action<T> action)
        {
            endpoints.MapAction((Delegate)action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<T1, T2>(
            this IEndpointRouteBuilder endpoints,
            Action<T1, T2> action)
        {
            endpoints.MapAction((Delegate)action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<T1, T2, T3>(
            this IEndpointRouteBuilder endpoints,
            Action<T1, T2, T3> action)
        {
            endpoints.MapAction((Delegate)action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<T1, T2, T3, T4>(
            this IEndpointRouteBuilder endpoints,
            Action<T1, T2, T3, T4> action)
        {
            endpoints.MapAction((Delegate)action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<TResult>(
            this IEndpointRouteBuilder endpoints,
            Func<TResult> action)
        {
            endpoints.MapAction((Delegate)action);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<T, TResult>(
            this IEndpointRouteBuilder endpoints,
            Func<T, TResult> action)
        {
            endpoints.MapAction((Delegate)action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<T1, T2, TResult>(
            this IEndpointRouteBuilder endpoints,
            Func<T1, T2, TResult> action)
        {
            endpoints.MapAction((Delegate)action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<T1, T2, T3, TResult>(
            this IEndpointRouteBuilder endpoints,
            Func<T1, T2, T3, TResult> action)
        {
            endpoints.MapAction((Delegate)action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoints"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static void MapAction<T1, T2, T3, T4, TResult>(
            this IEndpointRouteBuilder endpoints,
            Func<T1, T2, T3, T4, TResult> action)
        {
            endpoints.MapAction((Delegate)action);
        }

        #endregion

        private class MapActionModelProvider : IApplicationModelProvider
        {
            public List<Delegate> Actions { get; set; } = new();

            public int Order => -1000;

            public void OnProvidersExecuting(ApplicationModelProviderContext context)
            {
                var defaultProvider = new DefaultApplicationModelProvider(Options.Create(new MvcOptions()), null);
                var controllerTypeInfo = typeof(MapActionController).GetTypeInfo();
                var controllerModel = defaultProvider.CreateControllerModel(controllerTypeInfo);

                foreach (var action in Actions)
                {
                    var actionMethodInfo = action.GetMethodInfo();
                    var actionModel = defaultProvider.CreateActionModel(controllerTypeInfo, actionMethodInfo);
                    actionModel.Controller = controllerModel;
                    actionModel.Delegate = action;

                    foreach (var parameterInfo in actionModel.ActionMethod.GetParameters())
                    {
                        var parameterModel = defaultProvider.CreateParameterModel(parameterInfo);
                        if (parameterModel != null)
                        {
                            parameterModel.Action = actionModel;
                            actionModel.Parameters.Add(parameterModel);
                        }
                    }

                    controllerModel.Actions.Add(actionModel);
                }

                context.Result.Controllers.Add(controllerModel);
            }

            public void OnProvidersExecuted(ApplicationModelProviderContext context)
            {
                // Intentionally empty.
            }

            private class MapActionController { }
        }

        private class CustomMethodInfo : MethodInfo
        {
            public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotImplementedException();

            public override MethodAttributes Attributes => throw new NotImplementedException();

            public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();

            public override Type DeclaringType => throw new NotImplementedException();

            public override string Name => throw new NotImplementedException();

            public override Type ReflectedType => throw new NotImplementedException();

            public override MethodInfo GetBaseDefinition()
            {
                throw new NotImplementedException();
            }

            public override object[] GetCustomAttributes(bool inherit)
            {
                throw new NotImplementedException();
            }

            public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            {
                throw new NotImplementedException();
            }

            public override MethodImplAttributes GetMethodImplementationFlags()
            {
                throw new NotImplementedException();
            }

            public override ParameterInfo[] GetParameters()
            {
                throw new NotImplementedException();
            }

            public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            {
                throw new NotImplementedException();
            }

            public override bool IsDefined(Type attributeType, bool inherit)
            {
                throw new NotImplementedException();
            }
        }
    }
}
