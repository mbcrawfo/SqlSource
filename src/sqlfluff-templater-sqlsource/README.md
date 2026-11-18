# sqlfluff-templater-sqlsource

A [sqlfluff](https://sqlfluff.com) templater for [SqlSource](https://github.com/mbcrawfo/SqlSource) queries. It lets
sqlfluff lint and format the `.sql` files of a SqlSource project, with their `{{...}}` tokens and `-- name:` headers
left as they are.

## Install

```sh
pip install sqlfluff sqlfluff-templater-sqlsource==<version>
```

Replace `<version>` with the version of your `SqlSource` package, as
[the guide](https://mbcrawfo.github.io/SqlSource/guides/sqlfluff.html#set-up-sqlfluff) says.

## Use

Select the templater and your database's sqlfluff dialect in the `.sqlfluff` file of the folder you run sqlfluff
from:

```ini
[sqlfluff]
templater = sqlsource
dialect = postgres
```

The guide at <https://mbcrawfo.github.io/SqlSource/guides/sqlfluff.html> describes what sqlfluff then checks and how
to configure it.
