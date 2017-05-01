export interface Middleware {
    send(message: string, next: (message: string) => void) : void;
    receive(message: string, next: (message: string) => void) : void;
}