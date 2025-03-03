# OpenIddict.AmazonDynamoDB

![Build Status](https://github.com/vruss/OpenIddict.NHibernate/actions/workflows/ci-cd.yml/badge.svg) [![codecov](https://codecov.io/gh/vruss/OpenIddict.NHibernate/branch/main/graph/badge.svg?token=TODO)](https://codecov.io/gh/vruss/OpenIddict.NHibernate) [![NuGet](https://img.shields.io/nuget/v/Community.OpenIddict.NHibernate)](https://www.nuget.org/packages/Community.OpenIddict.NHibernate)

A [NHibernate](https://nhibernate.info/) integration for [OpenIddict](https://github.com/openiddict/openiddict-core).

## Getting Started

You can install the latest version via [Nuget](https://www.nuget.org/packages/Community.OpenIddict.NHibernate):

```
> dotnet add package Community.OpenIddict.NHibernate
```

Then you use the stores by calling `AddNHibernateStores` on `OpenIddictBuilder`:

```c#
services
    .AddOpenIddict()
    .AddCore()
    .UseNHibernate()
    .~~~~Configure(options =>
    {
        options.BillingMode = BillingMode.PROVISIONED; // Default is BillingMode.PAY_PER_REQUEST
        options.ProvisionedThroughput = new ProvisionedThroughput
        {
            ReadCapacityUnits = 5, // Default is 1
            WriteCapacityUnits = 5, // Default is 1
        };
        options.UsersTableName = "CustomOpenIddictTable"; // Default is openiddict
    });
```

Finally, you need to ensure that tables and indexes have been added:

```c#
OpenIddictNHibernateSetup.EnsureInitialized(serviceProvider);
```

Or asynchronously:

```c#
await OpenIddictNHibernateSetup.EnsureInitializedAsync(serviceProvider);
```


## Tests

In order to run the tests, you need to have NHibernate running locally on `localhost:8000`. This can easily be done using [Docker](https://www.docker.com/) and the following command:

```
docker run -p 8000:8000 local~~~~/NHibernate-local
```
