export class InvocationDescriptor {
    public MethodName: string;
    public Arguments: Array<any>;

    constructor(methodName: string, args: any[]) {
        this.MethodName = methodName;
        this.Arguments = args;
    }
}