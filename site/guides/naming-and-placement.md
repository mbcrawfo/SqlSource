# Change names, namespaces and visibility

<a id="model-namespace-task"></a>

## The namespace of the models

By default the models are in the namespace of the type that claims the query.  Set `"modelNamespace": "App.Models"` in the `sqlsource.json` beside your project file, or `-- model-namespace:` in a `.sql` file.  Two types in two namespaces that read one table each get a model of it, one in each namespace, and the setting gives them one.  A name with a period in an `-- output-model:` line, `App.Rows.UserRow`, is a full name and ignores the setting.  See [namespace of the models](../reference/models.md#setting-model-namespace).
