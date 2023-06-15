namespace Surab.Compilation;

internal enum OpCode : ushort
{
    /// <summary>
    /// Return from the current context.
    /// </summary>
    ret = 0x0001,

    /// <summary>
    /// Pop the topmost value from the stack.
    /// </summary>
    pop = 0x0002,

    /// <summary>
    /// Add two numbers.
    /// Input:
    ///   - 4 bytes on the stack: First number
    ///   - 4 bytes on the stack: Second number
    /// Output:
    ///   - 4 bytes on the stack: The addition result
    /// </summary>
    add = 0x0010,
    /// <summary>
    /// Subtract two numbers.
    /// Input:
    ///   - 4 bytes on the stack: First number
    ///   - 4 bytes on the stack: Second number
    /// Output:
    ///   - 4 bytes on the stack: The subtraction result
    /// </summary>
    sub = 0x0011,

    /// <summary>
    /// Set the value of a local.
    /// Input:
    ///   - 4 bytes IP: The offset of the local
    ///   - 4 bytes on the stack: The value to set
    /// Output:
    ///   - 4 bytes on the stack: The value of the local
    /// </summary>
    stloc = 0x0020,
    /// <summary>
    /// Load the value of a local.
    /// Input:
    ///   - 4 bytes IP: The offset of the local
    /// Output:
    ///   - 4 bytes on the stack: The value of the local
    /// </summary>
    ldloc = 0x0021,
    /// <summary>
    /// Load the value of a literal.
    /// Input:
    ///   - 4 bytes on the stack: The value of the literal
    /// Output:
    ///   - 4 bytes on the stack: The value of the literal
    /// </summary>
    ldlit = 0x0022,

    /// <summary>
    /// Prints the value of the topmost 4 bytes on the stack.
    /// This is a temp op.
    /// </summary>
    print = 0x1900,
}
