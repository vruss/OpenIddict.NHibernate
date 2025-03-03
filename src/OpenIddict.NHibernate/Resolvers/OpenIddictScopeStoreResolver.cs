using System;
using System.Collections.Concurrent;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Models;
using OpenIddict.NHibernate.Stores;

namespace OpenIddict.NHibernate.Resolvers
{
    /// <summary>
    /// Exposes a method allowing to resolve a scope store.
    /// </summary>
    public class OpenIddictScopeStoreResolver : IOpenIddictScopeStoreResolver
    {
        private readonly TypeResolutionCache _cache;
        private readonly IServiceProvider _provider;

        public OpenIddictScopeStoreResolver(
            [NotNull] TypeResolutionCache cache,
            [NotNull] IServiceProvider provider)
        {
            this._cache = cache;
            this._provider = provider;
        }

        /// <summary>
        /// Returns a scope store compatible with the specified scope type or throws an
        /// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
        /// </summary>
        /// <typeparam name="TScope">The type of the Scope entity.</typeparam>
        /// <returns>An <see cref="IOpenIddictScopeStore{TScope}"/>.</returns>
        public IOpenIddictScopeStore<TScope> Get<TScope>() where TScope : class
        {
            var store = this._provider.GetService<IOpenIddictScopeStore<TScope>>();
            if (store != null)
            {
                return store;
            }

            var type = this._cache.GetOrAdd(typeof(TScope), key =>
            {
                var root = OpenIddictHelpers.FindGenericBaseType(key, typeof(OpenIddictScope<>));
                if (root == null)
                {
                    throw new InvalidOperationException(new StringBuilder()
                        .AppendLine("The specified scope type is not compatible with the NHibernate stores.")
                        .Append("When enabling the NHibernate stores, make sure you use the built-in ")
                        .Append("'OpenIddictScope' entity (from the 'OpenIddict.NHibernate.Models' package) ")
                        .Append("or a custom entity that inherits from the generic 'OpenIddictScope' entity.")
                        .ToString());
                }

                return typeof(OpenIddictScopeStore<,>).MakeGenericType(
                    /* TScope: */ key,
                    /* TKey: */ root.GenericTypeArguments[0]);
            });

            return (IOpenIddictScopeStore<TScope>) this._provider.GetRequiredService(type);
        }

        // Note: NHibernate resolvers are registered as scoped dependencies as their inner
        // service provider must be able to resolve scoped services (typically, the store they return).
        // To avoid having to declare a static type resolution cache, a special cache service is used
        // here and registered as a singleton dependency so that its content persists beyond the scope.
        public class TypeResolutionCache : ConcurrentDictionary<Type, Type> { }
    }
}
