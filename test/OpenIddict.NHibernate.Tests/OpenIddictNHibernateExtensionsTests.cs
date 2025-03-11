using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.NHibernate.Models;
using OpenIddict.NHibernate.Stores;
using Xunit;

namespace OpenIddict.NHibernate.Tests
{
	public class OpenIddictNHibernateExtensionsTests
	{
		[Fact]
		public void UseNHibernate_ThrowsAnExceptionForNullBuilder()
		{
			// Arrange
			var builder = (OpenIddictCoreBuilder) null;

			// Act and assert
			var exception = Assert.Throws<ArgumentNullException>(() => builder.UseNHibernate());

			Assert.Equal("builder", exception.ParamName);
		}

		[Fact]
		public void UseNHibernate_ThrowsAnExceptionForNullConfiguration()
		{
			// Arrange
			var services = new ServiceCollection();
			var builder = new OpenIddictCoreBuilder(services);

			// Act and assert
			var exception = Assert.Throws<ArgumentNullException>(() => builder.UseNHibernate(configuration: null));

			Assert.Equal("configuration", exception.ParamName);
		}

		[Fact]
		public void UseNHibernate_RegistersDefaultEntities()
		{
			// Arrange
			var services = new ServiceCollection().AddOptions();
			var builder = new OpenIddictCoreBuilder(services);

			// Act
			builder.UseNHibernate();

			// Assert
			var provider = services.BuildServiceProvider();
			var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

			Assert.Equal(typeof(OpenIddictNHibernateApplication), options.DefaultApplicationType);
			Assert.Equal(typeof(OpenIddictNHibernateAuthorization), options.DefaultAuthorizationType);
			Assert.Equal(typeof(OpenIddictNHibernateScope), options.DefaultScopeType);
			Assert.Equal(typeof(OpenIddictNHibernateToken), options.DefaultTokenType);
		}

		[Theory]
		[InlineData(typeof(IOpenIddictApplicationStoreResolver), typeof(OpenIddict.NHibernate.Resolvers.OpenIddictNHibernateApplicationStoreResolver))]
		[InlineData(typeof(IOpenIddictAuthorizationStoreResolver), typeof(OpenIddict.NHibernate.Resolvers.OpenIddictNHibernateAuthorizationStoreResolver))]
		[InlineData(typeof(IOpenIddictScopeStoreResolver), typeof(OpenIddict.NHibernate.Resolvers.OpenIddictNHibernateScopeStoreResolver))]
		[InlineData(typeof(IOpenIddictTokenStoreResolver), typeof(OpenIddict.NHibernate.Resolvers.OpenIddictNHibernateTokenStoreResolver))]
		public void UseNHibernate_RegistersNHibernateStoreResolvers(Type serviceType, Type implementationType)
		{
			// Arrange
			var services = new ServiceCollection();
			var builder = new OpenIddictCoreBuilder(services);

			// Act
			builder.UseNHibernate();

			// Assert
			Assert.Contains(services, service => service.ServiceType == serviceType && service.ImplementationType == implementationType);
		}

		[Theory]
		[InlineData(typeof(OpenIddictNHibernateApplicationStore<,,,>))]
		[InlineData(typeof(OpenIddictNHibernateAuthorizationStore<,,,>))]
		[InlineData(typeof(OpenIddictNHibernateScopeStore<,>))]
		[InlineData(typeof(OpenIddictNHibernateTokenStore<,,,>))]
		public void UseNHibernate_RegistersNHibernateStore(Type type)
		{
			// Arrange
			var services = new ServiceCollection();
			var builder = new OpenIddictCoreBuilder(services);

			// Act
			builder.UseNHibernate();

			// Assert
			Assert.Contains(services, service => service.ServiceType == type && service.ImplementationType == type);
		}
	}
}
