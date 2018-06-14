export class InvocationDescriptor {
    private static CurrentId = 1;
    public Id: number;
    public MethodName: string;
    public Arguments: Array<any>;

    constructor(methodName: string, args: any[]) {
        this.Id = InvocationDescriptor.CurrentId++;
        this.MethodName = methodName;
        this.Arguments = args;
    }
}