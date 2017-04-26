export enum MessageType {
    Text = 0,
    MethodInvocation = 1,
    ConnectionEvent = 2
}

export class Message {
    public MessageType: MessageType;
    public Data: string;
}
