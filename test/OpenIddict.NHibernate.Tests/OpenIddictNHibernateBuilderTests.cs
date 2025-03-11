using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NHibernate;
using OpenIddict.Core;
using OpenIddict.NHibernate.Models;
using Xunit;

namespace OpenIddict.NHibernate.Tests
{
	public class OpenIddictNHibernateBuilderTests
	{
		[Fact]
		public void Constructor_ThrowsAnExceptionForNullServices()
		{
			// Arrange
			var services = (IServiceCollection) null;

			// Act and assert
			var exception = Assert.Throws<ArgumentNullException>(() => new OpenIddictNHibernateBuilder(services));

			Assert.Equal("services", exception.ParamName);
		}

		[Fact]
		public void ReplaceDefaultEntities_EntitiesAreCorrectlyReplaced()
		{
			// Arrange
			var services = CreateServices();
			var builder = CreateBuilder(services);

			// Act
			builder.ReplaceDefaultEntities<CustomNHibernateApplication, CustomNHibernateAuthorization, CustomNHibernateScope, CustomNHibernateToken, long>();

			// Assert
			var provider = services.BuildServiceProvider();
			var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

			Assert.Equal(typeof(CustomNHibernateApplication), options.DefaultApplicationType);
			Assert.Equal(typeof(CustomNHibernateAuthorization), options.DefaultAuthorizationType);
			Assert.Equal(typeof(CustomNHibernateScope), options.DefaultScopeType);
			Assert.Equal(typeof(CustomNHibernateToken), options.DefaultTokenType);
		}

		[Fact]
		public void ReplaceDefaultEntities_AllowsSpecifyingCustomKeyType()
		{
			// Arrange
			var services = CreateServices();
			var builder = CreateBuilder(services);

			// Act
			builder.ReplaceDefaultEntities<long>();

			// Assert
			var provider = services.BuildServiceProvider();
			var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictCoreOptions>>().CurrentValue;

			Assert.Equal(typeof(OpenIddictNHibernateApplication<long>), options.DefaultApplicationType);
			Assert.Equal(typeof(OpenIddictNHibernateAuthorization<long>), options.DefaultAuthorizationType);
			Assert.Equal(typeof(OpenIddictNHibernateScope<long>), options.DefaultScopeType);
			Assert.Equal(typeof(OpenIddictNHibernateToken<long>), options.DefaultTokenType);
		}

		[Fact]
		public void UseSessionFactory_ThrowsAnExceptionForNullFactory()
		{
			// Arrange
			var services = CreateServices();
			var builder = CreateBuilder(services);

			// Act and assert
			var exception = Assert.Throws<ArgumentNullException>(() => builder.UseSessionFactory(factory: null));

			Assert.Equal("factory", exception.ParamName);
		}

		[Fact]
		public void UseSessionFactory_SetsDbContextTypeInOptions()
		{
			// Arrange
			var services = CreateServices();
			var builder = CreateBuilder(services);
			var factory = Mock.Of<ISessionFactory>();

			// Act
			builder.UseSessionFactory(factory);

			// Assert
			var provider = services.BuildServiceProvider();
			var options = provider.GetRequiredService<IOptionsMonitor<OpenIddictNHibernateOptions>>().CurrentValue;

			Assert.Same(factory, options.SessionFactory);
		}

		private static OpenIddictNHibernateBuilder CreateBuilder(IServiceCollection services)
		{
			return services
				.AddOpenIddict()
				.AddCore()
				.UseNHibernate();
		}

		private static IServiceCollection CreateServices()
		{
			var services = new ServiceCollection();
			services.AddOptions();

			return services;
		}

		public class CustomNHibernateApplication : OpenIddictNHibernateApplication<long, CustomNHibernateAuthorization, CustomNHibernateToken> { }
		public class CustomNHibernateAuthorization : OpenIddictNHibernateAuthorization<long, CustomNHibernateApplication, CustomNHibernateToken> { }
		public class CustomNHibernateScope : OpenIddictNHibernateScope<long> { }
		public class CustomNHibernateToken : OpenIddictNHibernateToken<long, CustomNHibernateApplication, CustomNHibernateAuthorization> { }
	}
}
