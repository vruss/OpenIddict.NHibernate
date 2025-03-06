using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.NHibernate.Models;
using OpenIddict.NHibernate.Resolvers;
using OpenIddict.NHibernate.Stores;

namespace OpenIddict.NHibernate
{
	/// <summary>
	/// Exposes extensions allowing to register the OpenIddict NHibernate services.
	/// </summary>
	public static class OpenIddictNHibernateExtensions
	{
		/// <summary>
		/// Registers the NHibernate stores services in the DI container and
		/// configures OpenIddict to use the NHibernate entities by default.
		/// </summary>
		/// <param name="builder">The services builder used by OpenIddict to register new services.</param>
		/// <remarks>This extension can be safely called multiple times.</remarks>
		/// <returns>The <see cref="OpenIddictNHibernateBuilder"/>.</returns>
		public static OpenIddictNHibernateBuilder UseNHibernate(this OpenIddictCoreBuilder? builder)
		{
			if (builder == null)
			{
				throw new ArgumentNullException(nameof(builder));
			}

			// Since NHibernate may be used with databases performing case-insensitive or
			// culture-sensitive comparisons, ensure the additional filtering logic is enforced
			// in case case-sensitive stores were registered before this extension was called.
			builder.Configure(options => options.DisableAdditionalFiltering = false);

			builder
				.SetDefaultApplicationEntity<OpenIddictApplication>()
				.SetDefaultAuthorizationEntity<OpenIddictAuthorization>()
				.SetDefaultScopeEntity<OpenIddictScope>()
				.SetDefaultTokenEntity<OpenIddictToken>();

			builder
				.ReplaceApplicationStoreResolver<OpenIddictApplicationStoreResolver>()
				.ReplaceAuthorizationStoreResolver<OpenIddictAuthorizationStoreResolver>()
				.ReplaceScopeStoreResolver<OpenIddictScopeStoreResolver>()
				.ReplaceTokenStoreResolver<OpenIddictTokenStoreResolver>();

			builder.Services.TryAddSingleton<OpenIddictApplicationStoreResolver.TypeResolutionCache>();
			builder.Services.TryAddSingleton<OpenIddictAuthorizationStoreResolver.TypeResolutionCache>();
			builder.Services.TryAddSingleton<OpenIddictScopeStoreResolver.TypeResolutionCache>();
			builder.Services.TryAddSingleton<OpenIddictTokenStoreResolver.TypeResolutionCache>();

			builder.Services.TryAddScoped(typeof(OpenIddictApplicationStore<,,,>));
			builder.Services.TryAddScoped(typeof(OpenIddictAuthorizationStore<,,,>));
			builder.Services.TryAddScoped(typeof(OpenIddictScopeStore<,>));
			builder.Services.TryAddScoped(typeof(OpenIddictTokenStore<,,,>));

			builder.Services.TryAddScoped<IOpenIddictNHibernateContext, OpenIddictNHibernateContext>();

			return new OpenIddictNHibernateBuilder(builder.Services);
		}

		/// <summary>
		/// Registers the NHibernate stores services in the DI container and
		/// configures OpenIddict to use the NHibernate entities by default.
		/// </summary>
		/// <param name="builder">The services builder used by OpenIddict to register new services.</param>
		/// <param name="configuration">The configuration delegate used to configure the NHibernate services.</param>
		/// <remarks>This extension can be safely called multiple times.</remarks>
		/// <returns>The <see cref="OpenIddictCoreBuilder"/>.</returns>
		public static OpenIddictCoreBuilder UseNHibernate(this OpenIddictCoreBuilder? builder
			, Action<OpenIddictNHibernateBuilder>? configuration
		)
		{
			if (builder == null)
			{
				throw new ArgumentNullException(nameof(builder));
			}

			if (configuration == null)
			{
				throw new ArgumentNullException(nameof(configuration));
			}

			configuration(builder.UseNHibernate());

			return builder;
		}
	}
}
