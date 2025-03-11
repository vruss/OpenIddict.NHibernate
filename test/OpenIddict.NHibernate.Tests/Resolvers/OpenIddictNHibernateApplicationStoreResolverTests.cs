using System;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.NHibernate.Models;
using OpenIddict.NHibernate.Resolvers;
using OpenIddict.NHibernate.Stores;
using Xunit;
using static OpenIddict.NHibernate.Resolvers.OpenIddictNHibernateApplicationStoreResolver;

namespace OpenIddict.NHibernate.Tests.Resolvers
{
	public class OpenIddictNHibernateApplicationStoreResolverTests
	{
		[Fact]
		public void Get_ReturnsCustomStoreCorrespondingToTheSpecifiedTypeWhenAvailable()
		{
			// Arrange
			var services = new ServiceCollection();
			services.AddSingleton(Mock.Of<IOpenIddictApplicationStore<CustomApplication>>());

			var provider = services.BuildServiceProvider();
			var resolver = new OpenIddictNHibernateApplicationStoreResolver(new TypeResolutionCache(), provider);

			// Act and assert
			Assert.NotNull(resolver.Get<CustomApplication>());
		}

		[Fact]
		public void Get_ThrowsAnExceptionForInvalidEntityType()
		{
			// Arrange
			var services = new ServiceCollection();

			var provider = services.BuildServiceProvider();
			var resolver = new OpenIddictNHibernateApplicationStoreResolver(new TypeResolutionCache(), provider);

			// Act and assert
			var exception = Assert.Throws<InvalidOperationException>(() => resolver.Get<CustomApplication>());

			var expectedMessage = new StringBuilder()
				.AppendLine("The specified application type is not compatible with the NHibernate stores.")
				.Append("When enabling the NHibernate stores, make sure you use the built-in ")
				.Append("'OpenIddictApplication' entity (from the 'OpenIddict.NHibernate.Models' package) ")
				.Append("or a custom entity that inherits from the generic 'OpenIddictApplication' entity.")
				.ToString();

			Assert.Equal(expectedMessage, exception.Message);
		}

		[Fact]
		public void Get_ReturnsDefaultStoreCorrespondingToTheSpecifiedTypeWhenAvailable()
		{
			// Arrange
			var services = new ServiceCollection();
			services.AddSingleton(Mock.Of<IOpenIddictApplicationStore<CustomApplication>>());
			services.AddSingleton(CreateStore());

			var provider = services.BuildServiceProvider();
			var resolver = new OpenIddictNHibernateApplicationStoreResolver(new TypeResolutionCache(), provider);

			// Act and assert
			Assert.NotNull(resolver.Get<MyNHibernateApplication>());
		}

		private static OpenIddictNHibernateApplicationStore<MyNHibernateApplication, MyNHibernateAuthorization, MyNHibernateToken, long> CreateStore()
		{
			return new Mock<OpenIddictNHibernateApplicationStore<MyNHibernateApplication, MyNHibernateAuthorization, MyNHibernateToken, long>>(Mock.Of<IMemoryCache>()
				, Mock.Of<IOpenIddictNHibernateContext>()
				, Mock.Of<IOptionsMonitor<OpenIddictNHibernateOptions>>()
			)
			.Object;
		}

		public class CustomApplication { }

		public class MyNHibernateApplication : OpenIddictNHibernateApplication<long, MyNHibernateAuthorization, MyNHibernateToken> { }
		public class MyNHibernateAuthorization : OpenIddictNHibernateAuthorization<long, MyNHibernateApplication, MyNHibernateToken> { }
		public class MyNHibernateScope : OpenIddictNHibernateScope<long> { }
		public class MyNHibernateToken : OpenIddictNHibernateToken<long, MyNHibernateApplication, MyNHibernateAuthorization> { }
	}
}
