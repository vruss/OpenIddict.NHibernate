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
	/// Exposes a method allowing to resolve a token store.
	/// </summary>
	public class OpenIddictNHibernateTokenStoreResolver : IOpenIddictTokenStoreResolver
	{
		private readonly TypeResolutionCache cache;
		private readonly IServiceProvider provider;

		public OpenIddictNHibernateTokenStoreResolver(TypeResolutionCache cache
			, IServiceProvider provider
		)
		{
			this.cache = cache;
			this.provider = provider;
		}

		/// <summary>
		/// Returns a token store compatible with the specified token type or throws an
		/// <see cref="InvalidOperationException"/> if no store can be built using the specified type.
		/// </summary>
		/// <typeparam name="TToken">The type of the Token entity.</typeparam>
		/// <returns>An <see cref="IOpenIddictTokenStore{TToken}"/>.</returns>
		public IOpenIddictTokenStore<TToken> Get<TToken>()
			where TToken : class
		{
			var store = this.provider.GetService<IOpenIddictTokenStore<TToken>>();
			if (store != null)
			{
				return store;
			}

			var type = this.cache.GetOrAdd(typeof(TToken)
				, key =>
				{
					var root = OpenIddictHelpers.FindGenericBaseType(key, typeof(OpenIddictNHibernateToken<,,>));
					if (root == null)
					{
						var message = new StringBuilder()
							.AppendLine("The specified token type is not compatible with the NHibernate stores.")
							.Append("When enabling the NHibernate stores, make sure you use the built-in ")
							.Append("'OpenIddictToken' entity (from the 'OpenIddict.NHibernate.Models' package) ")
							.Append("or a custom entity that inherits from the generic 'OpenIddictToken' entity.")
							.ToString();

						throw new InvalidOperationException(message);
					}

					return typeof(OpenIddictNHibernateTokenStore<,,,>).MakeGenericType(/* TToken: */ key
						, /* TApplication: */ root.GenericTypeArguments[1]
						, /* TAuthorization: */ root.GenericTypeArguments[2]
						, /* TKey: */ root.GenericTypeArguments[0]
					);
				}
			);

			return (IOpenIddictTokenStore<TToken>)this.provider.GetRequiredService(type);
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
