"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var InvocationDescriptor = (function () {
    function InvocationDescriptor(methodName, args) {
        this.Id = InvocationDescriptor.CurrentId++;
        this.MethodName = methodName;
        this.Arguments = args;
    }
    return InvocationDescriptor;
}());
InvocationDescriptor.CurrentId = 1;
exports.InvocationDescriptor = InvocationDescriptor;
//# sourceMappingURL=InvocationDescriptor.js.map