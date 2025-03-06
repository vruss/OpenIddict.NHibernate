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

namespace OpenIddict.NHibernate.Tests.Resolvers
{
	public class OpenIddictScopeStoreResolverTests
	{
		[Fact]
		public void Get_ReturnsCustomStoreCorrespondingToTheSpecifiedTypeWhenAvailable()
		{
			// Arrange
			var services = new ServiceCollection();
			services.AddSingleton(Mock.Of<IOpenIddictScopeStore<CustomScope>>());

			var provider = services.BuildServiceProvider();
			var resolver = new OpenIddictScopeStoreResolver(new OpenIddictScopeStoreResolver.TypeResolutionCache(), provider);

			// Act and assert
			Assert.NotNull(resolver.Get<CustomScope>());
		}

		[Fact]
		public void Get_ThrowsAnExceptionForInvalidEntityType()
		{
			// Arrange
			var services = new ServiceCollection();

			var provider = services.BuildServiceProvider();
			var resolver = new OpenIddictScopeStoreResolver(new OpenIddictScopeStoreResolver.TypeResolutionCache(), provider);

			// Act and assert
			var exception = Assert.Throws<InvalidOperationException>(() => resolver.Get<CustomScope>());

			var expectedMessage = new StringBuilder()
				.AppendLine("The specified scope type is not compatible with the NHibernate stores.")
				.Append("When enabling the NHibernate stores, make sure you use the built-in ")
				.Append("'OpenIddictScope' entity (from the 'OpenIddict.NHibernate.Models' package) ")
				.Append("or a custom entity that inherits from the generic 'OpenIddictScope' entity.")
				.ToString();

			Assert.Equal(expectedMessage, exception.Message);
		}

		[Fact]
		public void Get_ReturnsDefaultStoreCorrespondingToTheSpecifiedTypeWhenAvailable()
		{
			// Arrange
			var services = new ServiceCollection();
			services.AddSingleton(Mock.Of<IOpenIddictScopeStore<CustomScope>>());
			services.AddSingleton(CreateStore());

			var provider = services.BuildServiceProvider();
			var resolver = new OpenIddictScopeStoreResolver(new OpenIddictScopeStoreResolver.TypeResolutionCache(), provider);

			// Act and assert
			Assert.NotNull(resolver.Get<MyScope>());
		}

		private static OpenIddictScopeStore<MyScope, long> CreateStore()
		{
			return new Mock<OpenIddictScopeStore<MyScope, long>>(Mock.Of<IMemoryCache>()
				, Mock.Of<IOpenIddictNHibernateContext>()
				, Mock.Of<IOptionsMonitor<OpenIddictNHibernateOptions>>()
			)
			.Object;
		}

		public class CustomScope { }

		public class MyApplication : OpenIddictApplication<long, MyAuthorization, MyToken> { }
		public class MyAuthorization : OpenIddictAuthorization<long, MyApplication, MyToken> { }
		public class MyScope : OpenIddictScope<long> { }
		public class MyToken : OpenIddictToken<long, MyApplication, MyAuthorization> { }
	}
}
