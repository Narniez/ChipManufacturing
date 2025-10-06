using System.Collections.Generic;
using UnityEngine;

public class CommandHistory 
{
    private readonly Stack<ICommand> _undo = new Stack<ICommand>();
    public void Do(ICommand command)
    {
        if (command == null) return;
        if (command.Execute())
            _undo.Push(command);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _undo.Pop().Undo();
    }

    public void Clear()
    {
        _undo.Clear();
    }
}
