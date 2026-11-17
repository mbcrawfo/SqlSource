# sqlfluff-templater-sqlsource

A [sqlfluff](https://sqlfluff.com) templater for [SqlSource](https://github.com/mbcrawfo/SqlSource) queries. It lets
sqlfluff lint and format the `.sql` files of a SqlSource project, with their `{{...}}` tokens and `-- name:` headers
left as they are.

## Install

```sh
pip install sqlfluff sqlfluff-templater-sqlsource
```

## Use

Select the templater in your `.sqlfluff` file:

```ini
[sqlfluff]
templater = sqlsource
```

The guide at <https://mbcrawfo.github.io/SqlSource/guides/sqlfluff.html> describes what sqlfluff then checks and how
to configure it.
