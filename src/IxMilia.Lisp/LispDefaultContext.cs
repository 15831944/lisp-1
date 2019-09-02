﻿using System;
using System.Linq;

namespace IxMilia.Lisp
{
    public class LispDefaultContext
    {
        [LispValue("defun")]
        public LispObject DefineFunction(LispHost host, LispObject[] args)
        {
            // TODO: properly validate types and arg counts
            var name = ((LispSymbol)args[0]).Value;
            var functionArgs = ((LispList)args[1]).Value.Cast<LispSymbol>().Select(a => a.Value);
            var commands = args.Skip(2);
            var function = new LispFunction(functionArgs, commands);
            host.SetValue(name, function);
            return host.Nil;
        }

        [LispValue("setq")]
        public LispObject SetValue(LispHost host, LispObject[] args)
        {
            // TODO: properly validate types
            for (int i = 0; i < args.Length - 1; i += 2)
            {
                var name = ((LispSymbol)args[i]).Value;
                var value = host.Eval(args[i + 1]);
                host.SetValue(name, value);
            }

            return host.Nil;
        }

        [LispValue("<")]
        public LispObject LessThan(LispHost host, LispObject[] args)
        {
            return Fold(host, args, (a, b) => a < b);
        }

        [LispValue("<=")]
        public LispObject LessThanOrEqual(LispHost host, LispObject[] args)
        {
            return Fold(host, args, (a, b) => a <= b);
        }

        [LispValue(">")]
        public LispObject GreaterThan(LispHost host, LispObject[] args)
        {
            return Fold(host, args, (a, b) => a > b);
        }

        [LispValue(">=")]
        public LispObject GreaterThanOrEqual(LispHost host, LispObject[] args)
        {
            return Fold(host, args, (a, b) => a >= b);
        }

        [LispValue("=")]
        public LispObject Equal(LispHost host, LispObject[] args)
        {
            return Fold(host, args, (a, b) => a == b);
        }

        [LispValue("!=")]
        public LispObject NotEqual(LispHost host, LispObject[] args)
        {
            return Fold(host, args, (a, b) => a != b);
        }

        [LispValue("&&")]
        public LispObject And(LispHost host, LispObject[] args)
        {
            return Fold(host, args, true, (a, b) => a && b);
        }

        [LispValue("||")]
        public LispObject Or(LispHost host, LispObject[] args)
        {
            return Fold(host, args, true, (a, b) => a || b);
        }

        [LispValue("if")]
        public LispObject If(LispHost host, LispObject[] args)
        {
            if (args.Length != 3)
            {
                return new LispError("Expected 3 arguments");
            }

            var condition = host.Eval(args[0]);
            var resultExpressions = condition.Equals(host.Nil)
                ? (LispList)args[2] // nil means follow the false path
                : (LispList)args[1]; // everything else is true
            // TODO: numerical 0 should probably follow the false path
            var result = host.Eval(resultExpressions.Value);
            return result;
        }

        [LispValue("+")]
        public LispObject Add(LispHost host, LispObject[] args)
        {
            return Fold(host, args, 0.0, (a, b) => a + b);
        }

        [LispValue("-")]
        public LispObject Subtract(LispHost host, LispObject[] args)
        {
            if (args.Length == 1)
            {
                // simple negation
                var value = host.Eval(args[0]);
                switch (value)
                {
                    case LispError error:
                        return error;
                    case LispNumber num:
                        return new LispNumber(num.Value * -1);
                    default:
                        return new LispError($"Expected type number but found {value.GetType()}");
                }
            }
            else
            {
                return Fold(host, args, 0.0, (a, b) => a - b, useFirstAsInit: true);
            }
        }

        [LispValue("*")]
        public LispObject Multiply(LispHost host, LispObject[] args)
        {
            return Fold(host, args, 1.0, (a, b) => a * b);
        }

        [LispValue("/")]
        public LispObject Divide(LispHost host, LispObject[] args)
        {
            return Fold(host, args, 1.0, (a, b) => a / b, useFirstAsInit: true);
        }

        private static LispObject Fold(LispHost host, LispObject[] args, double init, Func<double, double, double> operation, bool useFirstAsInit = false)
        {
            if (args.Length == 0)
            {
                return new LispError("Missing arguments");
            }

            double result;
            int skip;
            if (useFirstAsInit)
            {
                skip = 1;
                var first = host.Eval(args[0]);
                switch (first)
                {
                    case LispError error:
                        return error;
                    case LispNumber num:
                        result = num.Value;
                        break;
                    default:
                        return new LispError($"Expected type number but found {first.GetType()}");
                }
            }
            else
            {
                skip = 0;
                result = init;
            }

            foreach (var arg in args.Skip(skip))
            {
                var value = host.Eval(arg);
                switch (value)
                {
                    case LispError error:
                        return error;
                    case LispNumber num:
                        result = operation(result, num.Value);
                        break;
                    default:
                        return new LispError($"Expected type number but found {value.GetType()}");
                }
            }

            return new LispNumber(result);
        }

        private static LispObject Fold(LispHost host, LispObject[] args, bool init, Func<bool, bool, bool> operation)
        {
            if (args.Length == 0)
            {
                return new LispError("Missing arguments");
            }

            var result = init;
            foreach (var arg in args)
            {
                var value = host.Eval(arg);
                switch (value)
                {
                    case LispError error:
                        return error;
                    default:
                        // TODO: non zero
                        var next = value.Equals(host.Nil) ? false : true;
                        result = operation(result, next);
                        break;
                }
            }

            return result ? host.T : host.Nil;
        }

        private static LispObject Fold(LispHost host, LispObject[] args, Func<double, double, bool> operation)
        {
            if (args.Length < 2)
            {
                return new LispError("At least 2 arguments needed");
            }

            var value = host.Eval(args[0]);
            double lastValue;
            switch (value)
            {
                case LispNumber num:
                    lastValue = num.Value;
                    break;
                case LispError error:
                    return error;
                default:
                    return new LispError($"Expected type number but found {value.GetType()}");
            }

            foreach (var arg in args.Skip(1))
            {
                value = host.Eval(arg);
                switch (value)
                {
                    case LispNumber num:
                        var result = operation(lastValue, num.Value);
                        if (!result)
                        {
                            return host.Nil;
                        }
                        lastValue = num.Value;
                        break;
                    case LispError error:
                        return error;
                    default:
                        return new LispError($"Expected type number but found {value.GetType()}");
                }
            }

            return host.T;
        }
    }
}
