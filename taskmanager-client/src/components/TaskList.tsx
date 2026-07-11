import { useEffect, useState } from "react";

interface Task {
  id: number;
  title: string;
  description: string;
  isCompleted: boolean;
  cretedAt: string;
}

export default function TaskList() {
    const [tasks, setTasks] = useState<Task[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        fetch("https://localhost:7159/api/Tasks")
            .then((response) => response.json())
            .then((data) => { 
                setTasks(data);
                setLoading(false);
            })
            .catch((error) => {
                console.error("Error fetching tasks:", error);
                setLoading(false);
            });
    }, []);

    if (loading) return (
        <p className="text-gray-500">Cargando tareas...</p>
    )

    return (
         <div className="space-y-3">
            {tasks.map(task => (
                <div
                key={task.id}
                className="bg-white rounded-lg shadow p-4"
                >
                <h3 className="font-semibold text-gray-800">
                    {task.title}
                </h3>
                <p className="text-gray-500 text-sm">
                    {task.description}
                </p>
                <span className={`text-xs px-2 py-1 rounded
                    ${task.isCompleted
                    ? 'bg-green-100 text-green-700'
                    : 'bg-yellow-100 text-yellow-700'
                    }`}>
                    {task.isCompleted ? 'Completada' : 'Pendiente'}
                </span>
                </div>
            ))}
        </div>
    )
}