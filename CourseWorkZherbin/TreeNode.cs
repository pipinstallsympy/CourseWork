namespace CourseWorkZherbin;

public class TreeNode<T>
{
    public T Value { get; set; }
    public TreeNode<T>? Parent { get; set; } = null;
    public List<TreeNode<T>> Children { get; set; }

    public TreeNode(T value)
    {
        Value = value;
        Children = new List<TreeNode<T>>();
    }

    public void AddChild(TreeNode<T> child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}