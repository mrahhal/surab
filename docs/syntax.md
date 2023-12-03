# Syntax

To fix:

- Compilation unit vs Modules
- Mutable

General:

```cs
@import("another_file.surab")

alias Integer i32;
alias SomeDelegate fn (i32) i32;

const SOME_CONSTANT = 2;

static SOME_RESULT = {
    // Do some computation.
};

// Functions can exist out of classes, in which case they simply live in the namespace.
fn main() {
    var size = BitSize {
        bits = 24,
    };

    var greeter = Greeter.new("mrahhal");
    greeter.greet();

  // Can't call `greeter.Change_name` because `greeter` is an immutable reference.
  // You can mutate it only inside a mutation block:
    greeter mutable {
        greeter.change_name("asdd");
    };
}

@public
struct BitSize {
  bits: Number;
}

class Greeter {
    anotherField: Number = 1;
    foo: Foo;

    // `new` is not a special name, it's just a convention for the ctor.
    @public @static
    new(field name: String) { }

    @public
    greet() {
        Print("Greet " + this.name);
    }

    @public
    get_name(): String { // Return type will be inferred if omitted
        foreach (a in b) {
            @last
        }

        return this.name;
    }

    @public @mutable
    change_name(newName: String) {
        this.name = newName;
    }
}
```

Records (by value is optional):

```cs
// struct will be by value, class will be by reference
record [struct|class] Response(name: String, number: Number);

fn main() {
    var r = Response {
        name: "mrahhal",
        number: 3,
    };

    var updated = r with {
        number: 42,
    };
}
```

Flow analysis/typing:

```cs
var version = GetVersion(httpContext);
if (version.IsSome()) {
    // version is now known to not be null...
}
```

Script:

```cs
#script

// Top level execution

var a = 1;
var b = 2;

Print(add(a, b));

fn add(a: Number, b: Number) {
    return a + b;
}
```
