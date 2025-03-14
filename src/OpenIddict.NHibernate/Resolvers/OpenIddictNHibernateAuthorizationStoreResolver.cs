using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Extensions;
using OpenIddict.NHibernate.Models;
using OpenIddict.NHibernate.Stores;

namespace OpenIddict.NHibernate.Resolvers
{
	/// <summary>
	/// Exposes a method allowing to resolve an authorization store.
	/// </summary>
	public class OpenIddictNHibernateAuthorizationStoreResolver : IOpenIddictAuthorizationStoreResolver
	{
		private readonly TypeResolutionCache cache;
		private readonly IServiceProvider provider;

		public OpenIddictNHibernateAuthorizationStoreResolver(TypeResolutionCache cache
			, IServiceProvider provider
		)
		{
			this.cache = cache;
			this.provider = provider;
		}

		/// <summary>
		/// Returns an authorization store compatible with the specified authorization type or throws an
		/// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
		/// </summary>
		/// <typeparam name="TAuthorization">The type of the Authorization entity.</typeparam>
		/// <returns>An <see cref="IOpenIddictAuthorizationStore{TAuthorization}"/>.</returns>
		public IOpenIddictAuthorizationStore<TAuthorization> Get<TAuthorization>()
			where TAuthorization : class
		{
			var store = this.provider.GetService<IOpenIddictAuthorizationStore<TAuthorization>>();
			if (store != null)
			{
				return store;
			}

			var type = this.cache.GetOrAdd(typeof(TAuthorization), key =>
				{
					var root = OpenIddictHelpers.FindGenericBaseType(key, typeof(OpenIddictNHibernateAuthorization<,,>));
					if (root == null)
					{
						throw new InvalidOperationException(new StringBuilder()
							.AppendLine("The specified authorization type is not compatible with the NHibernate stores.")
							.Append("When enabling the NHibernate stores, make sure you use the built-in ")
							.Append("'OpenIddictAuthorization' entity (from the 'OpenIddict.NHibernate.Models' package) ")
							.Append("or a custom entity that inherits from the generic 'OpenIddictAuthorization' entity.")
							.ToString()
						);
					}

					return typeof(OpenIddictNHibernateAuthorizationStore<,,,>).MakeGenericType( /* TAuthorization: */ key
						, /* TApplication: */ root.GenericTypeArguments[1]
						, /* TToken: */ root.GenericTypeArguments[2]
						, /* TKey: */ root.GenericTypeArguments[0]
					);
				}
			);

			return (IOpenIddictAuthorizationStore<TAuthorization>)this.provider.GetRequiredService(type);
		}

		// Note: NHibernate resolvers are registered as scoped dependencies as their inner
		// service provider must be able to resolve scoped services (typically, the store they return).
		// To avoid having to declare a static type resolution cache, a special cache service is used
		// here and registered as a singleton dependency so that its content persists beyond the scope.
		public class TypeResolutionCache : ConcurrentDictionary<Type, Type>
		{
		}
	}
}
