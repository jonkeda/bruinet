# Todo: the ways between the screens

| Field | Value |
| --- | --- |
| Product | Todo |
| Where | The app on Windows and Android. On the web: all but Users and User form |
| Status | draft |

Nodes are screens, arrows are what a person taps. What happens on a screen before it lets go (a
refused Save, the question on Cancel) is in that screen's journeys, not here.

```mermaid
flowchart LR
  L[Todos] -- Add --> N[Todo form: New todo]
  N -- Save --> L
  N -- Cancel --> L
  L -- "a todo" --> D[Todo]
  D -- Back --> L
  D -- Edit --> E[Todo form: Edit todo]
  E -- Save --> D
  E -- Cancel --> D
  D -- Delete --> L
  L -- Users --> U[Users]
  U -- Back --> L
  U -- Add --> F[User form]
  U -- "a user" --> F
  F -- Save --> U
  F -- Cancel --> U
```

The app opens on Todos.
