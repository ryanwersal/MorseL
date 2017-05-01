import { InvocationDescriptor } from './InvocationDescriptor'
import { Message, MessageType } from './Message'
import { Middleware } from './Middleware'

export class Connection {

    public url: string;
    public connectionId: string;
    public enableLogging: boolean = false;

    protected message: Message;
    protected socket: WebSocket;

    protected middlewares: Middleware[] = [];

    public clientMethods: { [s: string]: Function; } = {};
    public connectionMethods: { [s: string]: Function; } = {};

    constructor(url: string, enableLogging: boolean=false) {
        this.url = url;
        
        this.enableLogging = enableLogging;

        this.connectionMethods['onConnected'] = () => {
            if(this.enableLogging) {
                console.log('Connected! connectionId: ' + this.connectionId);
            }
        }

        this.connectionMethods['onDisconnected'] = () => {
            if(this.enableLogging) {
                console.log('Connection closed from: ' + this.url);
            }
        }

        this.connectionMethods['onOpen'] = (socketOpenedEvent: any) => {
            if(this.enableLogging) {
                console.log('WebSockets connection opened!');
            }
        }
    }

    public addMiddleware(middleware: Middleware) {
        this.middlewares.push(middleware);
    }

    public start() {
        this.socket = new WebSocket(this.url);

        this.socket.onopen = (event: MessageEvent) => {
            this.connectionMethods['onOpen'].apply(this, event);
        };

        this.socket.onmessage = (event: MessageEvent) => {
            var index = 0;
            var delegate = (transformedData: string): void => {
                if (index < this.middlewares.length) {
                    this.middlewares[index++].receive(transformedData, delegate);
                } else {
                    this.message = JSON.parse(transformedData);

                    if (this.message.MessageType == MessageType.Text) {
                        if(this.enableLogging) {
                            console.log('Text message received. Message: ' + this.message.Data);
                        }
                    }

                    else if (this.message.MessageType == MessageType.MethodInvocation) {
                        let invocationDescriptor: InvocationDescriptor = JSON.parse(this.message.Data);

                        this.clientMethods[invocationDescriptor.MethodName].apply(this, invocationDescriptor.Arguments);
                    }

                    else if (this.message.MessageType == MessageType.ConnectionEvent) {
                        this.connectionId = this.message.Data;
                        this.connectionMethods['onConnected'].apply(this);
                    }
                }
            };
            delegate(event.data);
        }

        this.socket.onclose = (event: CloseEvent) => {
            this.connectionMethods['onDisconnected'].apply(this);
        }

        this.socket.onerror = (event: ErrorEvent) => {
            if(this.enableLogging) {
                console.log('Error data: ' + event.error);
            }
        }
    }

    public invoke(methodName: string, ...args: any[]) {
        let invocationDescriptor = new InvocationDescriptor(methodName, args);
        
        if(this.enableLogging) {
            console.log(invocationDescriptor);
        }

        var index = 0;
        var delegate = (transformedData: string): void => {
            if (index < this.middlewares.length) {
                this.middlewares[index++].send(transformedData, delegate);
            } else {
                this.socket.send(transformedData);
            }
        };
        delegate(JSON.stringify(invocationDescriptor));
    }
}